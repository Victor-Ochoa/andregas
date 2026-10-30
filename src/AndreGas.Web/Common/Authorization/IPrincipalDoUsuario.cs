using System.Security.Claims;

using AndreGas.Infrastructure.Identity;

namespace AndreGas.Web.Common.Authorization;

/// <summary>
/// Cria o <see cref="ClaimsPrincipal"/> do Identity (com as claims de multi-empresa) de um usuário
/// <b>sem</b> logá-lo. Usado pela impersonation: o super admin "entra como" o admin de uma empresa
/// reemitindo o cookie com o principal desse outro usuário.
/// </summary>
/// <remarks>
/// A implementação é uma camada fina sobre
/// <see cref="Microsoft.AspNetCore.Identity.IUserClaimsPrincipalFactory{TUser}"/> — reutilizar a
/// factory garante que o principal impersonado tenha exatamente as mesmas claims de tenancy
/// (<c>EmpresaId</c>/<c>EmpresaNome</c>/<c>EmpresaCor</c>/<c>EhSuperAdmin</c>) de um login normal.
/// </remarks>
public interface IPrincipalDoUsuario
{
    /// <summary>Cria o principal do Identity (com as claims de multi-empresa) de um usuário, sem logá-lo.</summary>
    Task<ClaimsPrincipal> CriarAsync(ApplicationUser usuario);
}