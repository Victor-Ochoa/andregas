using Mediator;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using RevendaApp.Infrastructure;
using RevendaApp.Infrastructure.Identity;
using RevendaApp.Infrastructure.Tenancy;

namespace RevendaApp.Web.Features.Central.UsuariosEmpresa;

/// <summary>
/// Lista os usuários de uma empresa na central. O Identity <b>não</b> tem filtro global por
/// empresa, por isso o filtro por <c>EmpresaId</c> é explícito aqui (funciona nos modos central e
/// de empresa) e a role vem de um join com <c>AspNetUserRoles</c>/<c>AspNetRoles</c>.
/// </summary>
public sealed class ListarUsuariosEmpresaQueryHandler(AppDbContext db, ITenantContext tenant)
    : IQueryHandler<ListarUsuariosEmpresaQuery, IReadOnlyList<UsuarioEmpresaItem>>
{
    public async ValueTask<IReadOnlyList<UsuarioEmpresaItem>> Handle(ListarUsuariosEmpresaQuery query, CancellationToken cancellationToken)
    {
        // Limite real de segurança: a política da rota não basta, o handler reforça no servidor.
        if (!tenant.EhSuperAdmin)
        {
            throw new InvalidOperationException("Somente o super administrador da central pode listar os usuários de uma empresa.");
        }

        var usuarios = await db.Users
            .AsNoTracking()
            .Where(u => u.EmpresaId == query.EmpresaId)
            .OrderBy(u => u.Nome ?? u.Email)
            .Select(u => new { u.Id, u.Nome, Email = u.Email ?? "", u.Ativo })
            .ToListAsync(cancellationToken);

        if (usuarios.Count == 0)
        {
            return [];
        }

        var ids = usuarios.Select(u => u.Id).ToList();

        // Role por usuário (um usuário tem uma única role neste sistema) num único join, restrito
        // aos usuários DESTA empresa — sem varrer AspNetUserRoles × AspNetRoles das outras.
        var papeisPorUsuario = await (from ur in db.Set<IdentityUserRole<Guid>>()
                                      join r in db.Set<ApplicationRole>() on ur.RoleId equals r.Id
                                      where ids.Contains(ur.UserId)
                                      select new { ur.UserId, NomeRole = r.Name })
            .ToDictionaryAsync(x => x.UserId, x => x.NomeRole, cancellationToken);

        return usuarios
            .Select(u => new UsuarioEmpresaItem(
                u.Id,
                u.Nome,
                u.Email,
                papeisPorUsuario.GetValueOrDefault(u.Id),
                u.Ativo))
            .ToList();
    }
}