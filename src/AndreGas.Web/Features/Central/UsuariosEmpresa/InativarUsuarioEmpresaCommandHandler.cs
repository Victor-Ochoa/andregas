using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Identity;
using AndreGas.Infrastructure.Tenancy;

using Mediator;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Central.UsuariosEmpresa;

/// <summary>
/// Inativa/reativa um usuário de uma empresa pela central.
/// </summary>
/// <remarks>
/// <para>
/// <b>Escopo do tenant:</b> o usuário é carregado com <c>EmpresaId == command.EmpresaId</c> na
/// própria consulta. Um id de usuário de outra empresa simplesmente não é encontrado — o handler
/// nunca aceita um <c>UsuarioId</c> avulso.
/// </para>
/// <para>
/// <b>Último administrador:</b> inativar o último <c>Admin</c> ativo deixaria a empresa sem quem a
/// administre (Configurações, estoque, usuários). O handler recusa e devolve mensagem pt-BR; para
/// inativá-lo é preciso antes promover/reativar outro administrador.
/// </para>
/// <para>
/// <b>Escolha do resultado:</b> a recusa volta como <see cref="InativarUsuarioEmpresaResult"/> em
/// vez de <c>bool</c> porque o motivo precisa chegar à UI (a brief pede "mensagem pt-BR").
/// </para>
/// </remarks>
public sealed class InativarUsuarioEmpresaCommandHandler(AppDbContext db, ITenantContext tenant)
    : ICommandHandler<InativarUsuarioEmpresaCommand, InativarUsuarioEmpresaResult>
{
    public async ValueTask<InativarUsuarioEmpresaResult> Handle(InativarUsuarioEmpresaCommand command, CancellationToken cancellationToken)
    {
        // Limite real de segurança: a política da rota não basta, o handler reforça no servidor.
        if (!tenant.EhSuperAdmin)
        {
            throw new InvalidOperationException("Somente o super administrador da central pode inativar usuários de uma empresa.");
        }

        // Filtro por empresa na própria consulta: id de usuário de outra empresa não é encontrado.
        var usuario = await db.Users
            .FirstOrDefaultAsync(u => u.Id == command.UsuarioId && u.EmpresaId == command.EmpresaId, cancellationToken);

        if (usuario is null)
        {
            return InativarUsuarioEmpresaResult.Falha("Usuário não encontrado nesta empresa.");
        }

        if (!command.Ativo && await EhUltimoAdminAtivoAsync(command.EmpresaId, usuario.Id, cancellationToken))
        {
            return InativarUsuarioEmpresaResult.Falha(
                "Não é possível inativar o último administrador ativo da empresa. "
                + "Crie ou reative outro administrador antes.");
        }

        usuario.Ativo = command.Ativo;
        await db.SaveChangesAsync(cancellationToken);

        return InativarUsuarioEmpresaResult.Ok();
    }

    /// <summary>
    /// Verdadeiro quando o usuário é <c>Admin</c> e não existe nenhum outro <c>Admin</c> ativo na
    /// mesma empresa. Usuários de empresa não têm <c>SuperAdmin</c>, então a contagem considera só
    /// a role <c>Admin</c>.
    /// </summary>
    private async Task<bool> EhUltimoAdminAtivoAsync(Guid empresaId, Guid usuarioId, CancellationToken cancellationToken)
    {
        var idsDeAdmin = await db.Set<IdentityUserRole<Guid>>()
            .Join(db.Set<ApplicationRole>(),
                ur => ur.RoleId,
                r => r.Id,
                (ur, r) => new { ur.UserId, r.Name })
            .Where(x => x.Name == ApplicationRole.Admin)
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);

        if (!idsDeAdmin.Contains(usuarioId))
        {
            // Não é administrador: inativar não deixa a empresa sem admin.
            return false;
        }

        var outrosAdminsAtivos = await db.Users
            .CountAsync(u => u.EmpresaId == empresaId
                && u.Ativo
                && u.Id != usuarioId
                && idsDeAdmin.Contains(u.Id), cancellationToken);

        return outrosAdminsAtivos == 0;
    }
}