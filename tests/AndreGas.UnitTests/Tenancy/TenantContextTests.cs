using System.Security.Claims;

using AndreGas.Web.Common.Tenancy;

using Microsoft.AspNetCore.Components.Authorization;

namespace AndreGas.UnitTests.Tenancy;

/// <summary>
/// Testes do <see cref="TenantContext"/>: leitura das claims de empresa e, principalmente, o
/// comportamento defensivo quando não há claims (central) ou quando o provider não está disponível
/// (SSR estático/seed) — deve devolver padrões seguros em vez de lançar.
/// </summary>
public class TenantContextTests
{
    private static TenantContext ComPrincipal(params Claim[] claims) =>
        new(new FakeAuthStateProvider(new ClaimsPrincipal(new ClaimsIdentity(claims, "Teste"))));

    [Fact]
    public void EmpresaId_DeveLerClaim_QuandoPresente()
    {
        var empresaId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var tenant = ComPrincipal(
            new Claim(TenantContext.ClaimEmpresaId, empresaId.ToString()),
            new Claim(TenantContext.ClaimEmpresaNome, "André Gás"),
            new Claim(TenantContext.ClaimEmpresaCor, "#0A1F44"),
            new Claim(TenantContext.ClaimEhSuperAdmin, "false"),
            new Claim(TenantContext.ClaimImpersonando, "true"));

        Assert.Equal(empresaId, tenant.EmpresaId);
        Assert.Equal("André Gás", tenant.NomeEmpresa);
        Assert.Equal("#0A1F44", tenant.CorPrimaria);
        Assert.False(tenant.EhSuperAdmin);
        Assert.True(tenant.Impersonando);
        Assert.Equal(empresaId, tenant.EmpresaIdRequerida("cadastrar produto"));
    }

    [Fact]
    public void SemClaims_DeveRetornarPadroesSeguros_ModoCentral()
    {
        var tenant = ComPrincipal();

        Assert.Null(tenant.EmpresaId);
        Assert.Equal(string.Empty, tenant.NomeEmpresa);
        Assert.Equal(string.Empty, tenant.CorPrimaria);
        Assert.False(tenant.EhSuperAdmin);
        Assert.False(tenant.Impersonando);
    }

    [Fact]
    public void EmpresaIdRequerida_DeveLancar_QuandoSemEmpresa()
    {
        var tenant = ComPrincipal(new Claim(TenantContext.ClaimEhSuperAdmin, "true"));

        var excecao = Assert.Throws<InvalidOperationException>(
            () => tenant.EmpresaIdRequerida("cadastrar produto"));

        Assert.Contains("exige uma empresa", excecao.Message);
    }

    [Fact]
    public void ProviderIndisponivel_DeveCairParaCentral_SemLancar()
    {
        // Reproduz "Do not call GetAuthenticationStateAsync outside of the DI scope for a Razor
        // component" (SSR estático/seed): o TenantContext deve tratar como central.
        var tenant = new TenantContext(new FakeAuthStateProvider(
            () => throw new InvalidOperationException(
                "Do not call GetAuthenticationStateAsync outside of the DI scope for a Razor component.")));

        Assert.Null(tenant.EmpresaId);
        Assert.Equal(string.Empty, tenant.NomeEmpresa);
        Assert.False(tenant.EhSuperAdmin);
    }

    /// <summary>Provider configurável: devolve um principal fixo ou lança, conforme o cenário.</summary>
    private sealed class FakeAuthStateProvider : AuthenticationStateProvider
    {
        private readonly Func<AuthenticationState> _obter;

        public FakeAuthStateProvider(ClaimsPrincipal principal)
            : this(() => new AuthenticationState(principal))
        {
        }

        public FakeAuthStateProvider(Func<AuthenticationState> obter) => _obter = obter;

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(_obter());
    }
}