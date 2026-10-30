using Mediator;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using RevendaApp.Infrastructure;
using RevendaApp.Infrastructure.Identity;
using RevendaApp.Infrastructure.Tenancy;

namespace RevendaApp.Web.Features.Usuarios.Listar;

public sealed class ListarUsuariosQueryHandler(AppDbContext db, ITenantContext tenant) : IQueryHandler<ListarUsuariosQuery, IReadOnlyList<UsuarioListItem>>
{
    public async ValueTask<IReadOnlyList<UsuarioListItem>> Handle(ListarUsuariosQuery query, CancellationToken cancellationToken)
    {
        // Exige empresa selecionada e lista apenas os usuários dela. O Identity NÃO tem filtro
        // global por empresa (não é entidade de domínio), por isso o filtro é explícito aqui.
        // O caminho do super admin da central (listar usuários de todas as empresas / de uma
        // empresa escolhida) é da Task 10.
        var empresaId = tenant.EmpresaIdRequerida("listar usuários");

        // A role do usuário vem do primeiro registro de AspNetUserRoles (um usuário tem uma única
        // role neste sistema). Join manual via IdentityUserRole<Guid> → ApplicationRole, pois o
        // ApplicationUser não expõe navegação de roles por padrão.
        // O join é RESTRITO aos usuários desta empresa: o Identity não tem filtro global de tenant,
        // então sem a restrição ele varreria os vínculos de todas as empresas a cada listagem.
        var idsDaEmpresa = db.Users
            .Where(u => u.EmpresaId == empresaId)
            .Select(u => u.Id);

        var rolesPorUsuario = await db.Set<IdentityUserRole<Guid>>()
            .Where(ur => idsDaEmpresa.Contains(ur.UserId))
            .Join(db.Set<ApplicationRole>(),
                ur => ur.RoleId,
                r => r.Id,
                (ur, r) => new { ur.UserId, NomeRole = r.Name })
            .ToListAsync(cancellationToken);

        var usuarios = await db.Users
            .Where(u => u.EmpresaId == empresaId)
            .OrderBy(u => u.Nome ?? u.Email)
            .Select(u => new { u.Id, u.Nome, Email = u.Email ?? "" })
            .ToListAsync(cancellationToken);

        return usuarios
            .Select(u => new UsuarioListItem(
                u.Id,
                u.Nome,
                u.Email,
                rolesPorUsuario.FirstOrDefault(x => x.UserId == u.Id)?.NomeRole))
            .ToList();
    }
}