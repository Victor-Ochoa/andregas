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

namespace RevendaApp.Web.Features.Central.UsuariosEmpresa;

/// <summary>
/// Cria um usuário vinculado a uma empresa escolhida na central. O <c>EmpresaId</c> é atribuído
/// explicitamente (o Identity não é entidade de domínio, o interceptor de tenancy não o cobre) e a
/// empresa precisa existir.
/// </summary>
/// <remarks>
/// <para>
/// <b>Papel:</b> a lista permitida é checada <b>aqui</b>, não só no validador. O validador roda no
/// pipeline do Mediator e é a mensagem amigável da UI, mas o handler é o limite real: aceitar
/// <c>SuperAdmin</c> criaria um usuário com <c>EmpresaId</c> <b>e</b> a role de super admin — o
/// principal resultante teria <c>EhSuperAdmin=true</c> e <c>ClaimEmpresaId</c>, passando tanto em
/// <c>ApenasSuperAdmin</c> quanto em todos os guardas <c>tenant.EhSuperAdmin</c>.
/// </para>
/// <para>
/// <b>Auditoria atômica:</b> a criação do usuário (via <see cref="UserManager{TUser}"/>, que salva
/// no mesmo <see cref="AppDbContext"/> scoped) e o <see cref="RegistroAuditoria"/> rodam na
/// <b>mesma transação</b> de <see cref="TransacaoComRetry"/>: uma falha de auditoria desfaz o
/// usuário, e um usuário não criado não deixa registro.
/// </para>
/// </remarks>
public sealed class CriarUsuarioEmpresaCommandHandler(
    AppDbContext db,
    ITenantContext tenant,
    IUsuarioWriter usuarioWriter,
    AuthenticationStateProvider? authStateProvider = null)
    : ICommandHandler<CriarUsuarioEmpresaCommand, CriarUsuarioEmpresaResult>
{
    public async ValueTask<CriarUsuarioEmpresaResult> Handle(CriarUsuarioEmpresaCommand command, CancellationToken cancellationToken)
    {
        // Limite real de segurança: a política da rota não basta, o handler reforça no servidor.
        if (!tenant.EhSuperAdmin)
        {
            throw new InvalidOperationException("Somente o super administrador da central pode criar usuários em uma empresa.");
        }

        // Lista fechada de papéis: recusa (fail loud) em vez de confiar no validador.
        if (!CriarUsuarioEmpresaCommandValidator.PapeisPermitidos.Contains(command.Papel))
        {
            throw new InvalidOperationException(
                $"O papel '{command.Papel}' não pode ser atribuído pela central. Use Administrador ou Vendedor.");
        }

        // A empresa alvo precisa existir: sem essa checagem, a FK Restrict estouraria com um erro
        // técnico em vez de uma mensagem de negócio.
        var empresaExiste = await db.Set<Empresa>().AnyAsync(e => e.Id == command.EmpresaId, cancellationToken);
        if (!empresaExiste)
        {
            return CriarUsuarioEmpresaResult.Falha("Empresa não encontrada.");
        }

        if (await usuarioWriter.EmailJaUsadoAsync(command.Email, cancellationToken))
        {
            return CriarUsuarioEmpresaResult.Falha("Já existe um usuário cadastrado com esse e-mail.");
        }

        return await TransacaoComRetry.ExecutarAsync(
            db,
            transacao => ExecutarNaTransacaoAsync(command, transacao, cancellationToken),
            cancellationToken);
    }

    private async Task<CriarUsuarioEmpresaResult> ExecutarNaTransacaoAsync(
        CriarUsuarioEmpresaCommand command,
        IDbContextTransaction transacao,
        CancellationToken cancellationToken)
    {
        // Cada tentativa recomeça limpa (obrigatório para o retry da execution strategy).
        db.ChangeTracker.Clear();

        var (usuarioId, erro) = await usuarioWriter.CriarComRoleAsync(
            command.Nome, command.Email, command.Papel, command.Senha, command.EmpresaId, cancellationToken);

        if (usuarioId is null)
        {
            // Sem commit: o Dispose da transação desfaz qualquer resíduo do Identity.
            return CriarUsuarioEmpresaResult.Falha(erro ?? "Não foi possível criar o usuário.");
        }

        var (usuarioNome, autorId) = await UsuarioAtual.ObterAsync(authStateProvider, db, cancellationToken);
        var detalhes = $"Usuário {command.Email} ({command.Nome}) criado com o papel {command.Papel}.";

        db.RegistrosAuditoria.Add(new RegistroAuditoria(
            command.EmpresaId,
            autorId,
            usuarioNome,
            RegistroAuditoria.Acoes.UsuarioCriado,
            detalhes,
            RegistroAuditoria.Origens.Central));

        await db.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);

        return CriarUsuarioEmpresaResult.Ok(usuarioId.Value);
    }
}