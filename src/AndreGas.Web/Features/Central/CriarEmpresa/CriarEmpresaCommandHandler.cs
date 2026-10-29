using AndreGas.Domain.Entities;
using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Identity;
using AndreGas.Infrastructure.Tenancy;
using AndreGas.Web.Features.Usuarios.Cadastrar;

using Mediator;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Central.CriarEmpresa;

/// <summary>
/// Cria a empresa e o seu primeiro administrador de forma <b>atômica</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Transação:</b> a empresa é inserida e salva primeiro (o Identity exige que
/// <c>AspNetUsers.EmpresaId</c> aponte para uma linha existente — FK Restrict), e o usuário é criado
/// pelo <see cref="UserManager{TUser}"/>, que usa o <b>mesmo</b> <see cref="AppDbContext"/> scoped.
/// Tudo roda dentro de <c>Database.BeginTransactionAsync()</c>: se o Identity falhar (e-mail
/// duplicado, senha fraca...), o <c>Rollback</c> desfaz o insert da empresa — não existe empresa
/// órfã, sem nenhum administrador.
/// </para>
/// <para>
/// <b>Identificador duplicado:</b> há uma checagem explícita antes (mensagem clara) e a falha do
/// índice único é tratada como erro de negócio (corrida entre dois super admins). O validador da
/// UI cobre o caso comum; esta é a defesa em profundidade.
/// </para>
/// </remarks>
public sealed class CriarEmpresaCommandHandler(AppDbContext db, ITenantContext tenant, IUsuarioWriter usuarioWriter)
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

        await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
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
                // Rollback explícito: a empresa já salva não pode sobreviver sem administrador.
                await transacao.RollbackAsync(cancellationToken);
                return CriarEmpresaResult.Falha(erro ?? "Não foi possível criar o administrador da empresa.");
            }

            await transacao.CommitAsync(cancellationToken);

            return CriarEmpresaResult.Ok(empresa.Id, adminId.Value);
        }
        catch (DbUpdateException ex) when (EhViolacaoDeIdentificador(ex))
        {
            // Corrida: outro super admin criou o mesmo slug entre a checagem e o insert.
            await transacao.RollbackAsync(cancellationToken);
            return CriarEmpresaResult.Falha($"Já existe uma empresa com o identificador '{identificador}'.");
        }
    }

    /// <summary>
    /// Detecta a violação do índice único de <c>Identificador</c> (Postgres 23505). Verificado pelo
    /// nome do índice/constraint para não engolir outras falhas de persistência.
    /// </summary>
    private static bool EhViolacaoDeIdentificador(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("Identificador", StringComparison.OrdinalIgnoreCase) == true;
}