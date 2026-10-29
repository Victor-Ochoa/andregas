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
    public void ApenasEmpresa_nao_aceita_usuario_sem_a_claim_mesmo_que_seja_super_admin()
    {
        // Controle negativo explícito: a política é sobre a claim, não sobre a role. Um principal
        // com a role SuperAdmin e sem EmpresaId (o super admin da central) NÃO satisfaz ApenasEmpresa.
        var politica = ObterPolitica(Politicas.ApenasEmpresa);
        var requisito = Assert.Single(politica.Requirements.OfType<ClaimsAuthorizationRequirement>());

        Assert.False(requisito.ClaimType == ApplicationRole.SuperAdmin);
        Assert.DoesNotContain(politica.Requirements, r => r is RolesAuthorizationRequirement);
    }
}