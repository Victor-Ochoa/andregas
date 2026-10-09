using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Identity;

using Mediator;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Usuarios.Listar;

public sealed class ListarUsuariosQueryHandler(AppDbContext db) : IQueryHandler<ListarUsuariosQuery, IReadOnlyList<UsuarioListItem>>
{
    public async ValueTask<IReadOnlyList<UsuarioListItem>> Handle(ListarUsuariosQuery query, CancellationToken cancellationToken)
    {
        // A role do usuário vem do primeiro registro de AspNetUserRoles (um usuário tem uma única
        // role neste sistema). Join manual via IdentityUserRole<Guid> → ApplicationRole, pois o
        // ApplicationUser não expõe navegação de roles por padrão.
        var rolesPorUsuario = await db.Set<IdentityUserRole<Guid>>()
            .Join(db.Set<ApplicationRole>(),
                ur => ur.RoleId,
                r => r.Id,
                (ur, r) => new { ur.UserId, NomeRole = r.Name })
            .ToListAsync(cancellationToken);

        var usuarios = await db.Users
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