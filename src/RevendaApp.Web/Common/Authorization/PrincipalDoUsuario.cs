using System.Security.Claims;

using Microsoft.AspNetCore.Identity;

using RevendaApp.Infrastructure.Identity;

namespace RevendaApp.Web.Common.Authorization;

/// <summary>
/// Implementação fina de <see cref="IPrincipalDoUsuario"/> sobre
/// <see cref="IUserClaimsPrincipalFactory{TUser}"/>. Como o DI resolve a
/// <see cref="ApplicationUserClaimsPrincipalFactory"/> (registrada em <c>Program.cs</c>), o
/// principal criado aqui carrega as claims de multi-empresa exatamente como no login normal.
/// </summary>
public sealed class PrincipalDoUsuario(IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory)
    : IPrincipalDoUsuario
{
    public Task<ClaimsPrincipal> CriarAsync(ApplicationUser usuario) => claimsFactory.CreateAsync(usuario);
}