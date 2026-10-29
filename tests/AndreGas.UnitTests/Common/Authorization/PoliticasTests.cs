using System.Security.Claims;

using AndreGas.Infrastructure.Identity;
using AndreGas.Web.Common.Authorization;
using AndreGas.Web.Common.Tenancy;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace AndreGas.UnitTests.Common.Authorization;

/// <summary>
/// Testes das políticas declarativas de <see cref="Politicas"/>: garantem que a política de
/// super admin exige a role <see cref="ApplicationRole.SuperAdmin"/> e que a de empresa exige a
/// claim <see cref="TenantContext.ClaimEmpresaId"/> (o que impede o super admin sem empresa de
/// cair numa página de domínio e estourar 500).
/// </summary>
public class PoliticasTests
{
    private static AuthorizationPolicy ObterPolitica(string nome)
    {
        var options = new AuthorizationOptions();
        Politicas.AdicionarPoliticas(options);

        Assert.True(options.GetPolicy(nome) is not null, $"A política '{nome}' deveria estar registrada.");
        return options.GetPolicy(nome)!;
    }

    [Fact]
    public void ApenasSuperAdmin_exige_a_role_super_admin()
    {
        var politica = ObterPolitica(Politicas.ApenasSuperAdmin);

        Assert.Contains(politica.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
        var requisito = Assert.Single(politica.Requirements.OfType<RolesAuthorizationRequirement>());
        Assert.Equal([ApplicationRole.SuperAdmin], requisito.AllowedRoles);
    }

    [Fact]
    public void ApenasEmpresa_exige_a_claim_de_empresa()
    {
        var politica = ObterPolitica(Politicas.ApenasEmpresa);

        Assert.Contains(politica.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
        var requisito = Assert.Single(politica.Requirements.OfType<ClaimsAuthorizationRequirement>());
        Assert.Equal(TenantContext.ClaimEmpresaId, requisito.ClaimType);
    }

    [Fact]
    public async Task ApenasEmpresa_nega_super_admin_sem_claim_de_empresa()
    {
        // Avalia a política de verdade: um principal com a role SuperAdmin e SEM a claim EmpresaId
        // (o super admin da central) NÃO satisfaz ApenasEmpresa — é o que impede a página de
        // domínio de estourar EmpresaIdRequerida com 500.
        var politica = ObterPolitica(Politicas.ApenasEmpresa);

        var superAdmin = PrincipalCom(
            new Claim(ClaimTypes.Role, ApplicationRole.SuperAdmin),
            new Claim(TenantContext.ClaimEhSuperAdmin, "true"));

        Assert.False(await SatisfazAsync(politica, superAdmin));

        // Controle positivo: o mesmo principal COM a claim EmpresaId passa.
        var usuarioDeEmpresa = PrincipalCom(
            new Claim(ClaimTypes.Role, ApplicationRole.SuperAdmin),
            new Claim(TenantContext.ClaimEhSuperAdmin, "false"),
            new Claim(TenantContext.ClaimEmpresaId, Guid.NewGuid().ToString()));

        Assert.True(await SatisfazAsync(politica, usuarioDeEmpresa));
    }

    /// <summary>
    /// Executa os requisitos da política contra um principal, como o middleware faria. Os
    /// requisitos embutidos do ASP.NET Core (<see cref="DenyAnonymousAuthorizationRequirement"/>,
    /// <see cref="ClaimsAuthorizationRequirement"/>, <see cref="RolesAuthorizationRequirement"/>)
    /// implementam <see cref="IAuthorizationHandler"/> — os handlers dedicados são <c>internal</c>.
    /// </summary>
    private static async Task<bool> SatisfazAsync(AuthorizationPolicy politica, ClaimsPrincipal principal)
    {
        var contexto = new AuthorizationHandlerContext(politica.Requirements, principal, resource: null);

        foreach (var requisito in politica.Requirements.OfType<IAuthorizationHandler>())
        {
            await requisito.HandleAsync(contexto);
        }

        return contexto.HasSucceeded;
    }

    private static ClaimsPrincipal PrincipalCom(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "Identity.Application"));
}