using Mediator;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using RevendaApp.Domain.Entities;
using RevendaApp.Infrastructure;
using RevendaApp.Infrastructure.Identity;
using RevendaApp.Infrastructure.Tenancy;
using RevendaApp.Web.Common;
using RevendaApp.Web.Features.Usuarios.Cadastrar;

namespace RevendaApp.Web.Features.Central.CriarEmpresa;

/// <summary>
/// Cria a empresa e o seu primeiro administrador de forma <b>atômica</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Transação:</b> a empresa é inserida e salva primeiro (o Identity exige que
/// <c>AspNetUsers.EmpresaId</c> aponte para uma linha existente — FK Restrict), e o usuário é criado
/// pelo <see cref="UserManager{TUser}"/>, que usa o <b>mesmo</b> <see cref="AppDbContext"/> scoped.
/// Tudo roda dentro de <see cref="TransacaoComRetry"/>: se o Identity falhar (e-mail duplicado,
/// senha fraca...), o rollback desfaz o insert da empresa — não existe empresa órfã, sem nenhum
/// administrador.
/// </para>
/// <para>
/// <b>Execution strategy (crítico):</b> a transação é aberta <b>dentro</b> de
/// <c>Database.CreateExecutionStrategy().ExecuteAsync(...)</c>, nunca com um
/// <c>BeginTransactionAsync</c> solto. Em produção o <c>EnrichNpgsqlDbContext</c> liga
/// <c>EnableRetryOnFailure()</c> e a estratégia de retry <b>rejeita</b> transações abertas por fora
/// (lança <c>InvalidOperationException</c>, que não é <c>DbUpdateException</c> e mataria o
/// circuito). Ver <see cref="TransacaoComRetry"/>.
/// </para>
/// <para>
/// <b>Sem entidade órfã no change tracker:</b> o <see cref="AppDbContext"/> é scoped ao circuito,
/// então uma <c>Empresa</c> que ficou <c>Added</c> após um rollback seria inserida de novo no
/// próximo <c>SaveChanges</c> (o super admin corrige o e-mail e clica Salvar → duas empresas, uma
/// órfã). Por isso <b>todo</b> caminho de falha desanexa a entidade, e cada tentativa começa com o
/// change tracker limpo (obrigatório também para o retry da execution strategy).
/// </para>
/// <para>
/// <b>Identificador duplicado:</b> há uma checagem explícita antes (mensagem clara) e a falha do
/// índice único é tratada como erro de negócio (corrida entre dois super admins). O validador da
/// UI cobre o caso comum; esta é a defesa em profundidade.
/// </para>
/// </remarks>
public sealed class CriarEmpresaCommandHandler(
    AppDbContext db,
    ITenantContext tenant,
    IUsuarioWriter usuarioWriter,
    AuthenticationStateProvider? authStateProvider = null)
    : ICommandHandler<CriarEmpresaCommand, CriarEmpresaResult>
{
    public async ValueTask<CriarEmpresaResult> Handle(CriarEmpresaCommand command, CancellationToken cancellationToken)
    {
        // Limite real de segurança: a política da rota não basta, o handler reforça no servidor.
        if (!tenant.EhSuperAdmin)
        {
            throw new InvalidOperationException("Somente o super administrador da central pode cadastrar empresas.");
        }

        var identificador = command.Identificador.Trim();

        if (await db.Set<Empresa>().AnyAsync(e => e.Identificador == identificador, cancellationToken))
        {
            return CriarEmpresaResult.Falha($"Já existe uma empresa com o identificador '{identificador}'.");
        }

        if (await usuarioWriter.EmailJaUsadoAsync(command.Email, cancellationToken))
        {
            return CriarEmpresaResult.Falha("Já existe um usuário cadastrado com esse e-mail.");
        }

        return await TransacaoComRetry.ExecutarAsync(
            db,
            transacao => ExecutarNaTransacaoAsync(command, identificador, transacao, cancellationToken),
            cancellationToken);
    }

    private async Task<CriarEmpresaResult> ExecutarNaTransacaoAsync(
        CriarEmpresaCommand command,
        string identificador,
        IDbContextTransaction transacao,
        CancellationToken cancellationToken)
    {
        // Cada tentativa começa do zero: sem isso, a Empresa da tentativa anterior (Added) seria
        // inserida outra vez e o retry da execution strategy produziria duas empresas.
        db.ChangeTracker.Clear();

        var empresa = new Empresa(
            identificador,
            command.Nome,
            command.Telefones,
            command.Cnpj,
            command.Endereco,
            command.Numero,
            command.Cep,
            command.Bairro,
            command.Cidade,
            command.Estado,
            command.CorPrimaria,
            command.Logo);

        try
        {
            db.Set<Empresa>().Add(empresa);
            await db.SaveChangesAsync(cancellationToken);

            var (adminId, erro) = await usuarioWriter.CriarComRoleAsync(
                command.NomeAdmin,
                command.Email,
                ApplicationRole.Admin,
                command.Senha,
                empresa.Id,
                cancellationToken);

            if (adminId is null)
            {
                // Sem commit: o Dispose da transação faz o rollback. A empresa já salva não pode
                // sobreviver sem administrador.
                DesanexarEmpresa(empresa);
                return CriarEmpresaResult.Falha(erro ?? "Não foi possível criar o administrador da empresa.");
            }

            // Auditoria DENTRO da transação: uma falha no commit desfaz empresa, admin e registro
            // juntos. O retry recomeça com o change tracker limpo (acima), então o registro da
            // tentativa anterior nunca é inserido duas vezes.
            var (usuarioNome, usuarioId) = await UsuarioAtual.ObterAsync(authStateProvider, db, cancellationToken);
            db.RegistrosAuditoria.Add(new RegistroAuditoria(
                empresa.Id,
                usuarioId,
                usuarioNome,
                RegistroAuditoria.Acoes.EmpresaCriada,
                $"Empresa '{empresa.Nome}' ({identificador}) criada com o administrador {command.Email}.",
                RegistroAuditoria.Origens.Central));
            await db.SaveChangesAsync(cancellationToken);

            await transacao.CommitAsync(cancellationToken);
            return CriarEmpresaResult.Ok(empresa.Id, adminId.Value);
        }
        catch (DbUpdateException ex) when (EhViolacaoDeIdentificador(ex))
        {
            // Corrida: outro super admin criou o mesmo slug entre a checagem e o insert.
            DesanexarEmpresa(empresa);
            return CriarEmpresaResult.Falha($"Já existe uma empresa com o identificador '{identificador}'.");
        }
    }

    /// <summary>
    /// Desanexa a empresa (e o usuário parcial que o Identity possa ter deixado no tracker) para
    /// que a tentativa seguinte não reinsira a linha já desfeita pelo rollback.
    /// </summary>
    private void DesanexarEmpresa(Empresa empresa)
    {
        db.Entry(empresa).State = EntityState.Detached;

        // Falha do Identity pode deixar o ApplicationUser em Added/Modified no mesmo tracker.
        foreach (var entrada in db.ChangeTracker.Entries<ApplicationUser>().ToList())
        {
            entrada.State = EntityState.Detached;
        }
    }

    /// <summary>
    /// Detecta a violação do índice único de <c>Identificador</c> (Postgres 23505). Verificado pelo
    /// nome do índice/constraint para não engolir outras falhas de persistência.
    /// </summary>
    private static bool EhViolacaoDeIdentificador(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("Identificador", StringComparison.OrdinalIgnoreCase) == true;
}