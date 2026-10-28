using System.Security.Claims;

using AndreGas.Web.Common.Tenancy;

using Microsoft.AspNetCore.Components.Authorization;

namespace AndreGas.UnitTests.Tenancy;

/// <summary>
/// Testes do <see cref="TenantContext"/>: leitura das claims de empresa e, sobretudo, a regra de
/// segurança <b>fail-closed</b> — só é "modo central" (todas as empresas) quando NÃO autenticado
/// (seed/SSR/design-time) ou quando o usuário é explicitamente super admin. Usuário autenticado sem
/// empresa e sem ser super admin NÃO pode virar acesso a todos os tenants, e exceções inesperadas do
/// provider devem propagar.
/// </summary>
public class TenantContextTests
{
    private static TenantContext ComPrincipal(params Claim[] claims) =>
        new(new FakeAuthStateProvider(() => new AuthenticationState(
            new ClaimsPrincipal(new ClaimsIdentity(claims, "Teste")))));

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
    public void NaoAutenticado_DeveSerModoCentral_PadroesSeguros()
    {
        // Identidade SEM tipo de autenticação => IsAuthenticated == false (ex.: seed/anônimo).
        var tenant = new TenantContext(new FakeAuthStateProvider(() => new AuthenticationState(
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(TenantContext.ClaimEmpresaId, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")])))));

        Assert.Null(tenant.EmpresaId);
        Assert.Equal(string.Empty, tenant.NomeEmpresa);
        Assert.Equal(string.Empty, tenant.CorPrimaria);
        Assert.False(tenant.EhSuperAdmin);
        Assert.False(tenant.Impersonando);
    }

    [Fact]
    public void SuperAdminSemEmpresa_DeveSerModoCentral()
    {
        // Super admin da central: autenticado, sem EmpresaId, mas com EhSuperAdmin=true → central.
        var tenant = new TenantContext(new FakeAuthStateProvider(() => new AuthenticationState(
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(TenantContext.ClaimEhSuperAdmin, "true")], "Teste")))));

        Assert.True(tenant.EhSuperAdmin);
        Assert.Null(tenant.EmpresaId);
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
    public void AutenticadoSemEmpresaENaoSuperAdmin_DeveLancar_NaoVazaTodasAsEmpresas()
    {
        // Usuário autenticado, sem claim EmpresaId e sem ser super admin → configuração inválida.
        // NÃO pode degradar para central (isso leria todas as empresas).
        var tenant = new TenantContext(new FakeAuthStateProvider(() => new AuthenticationState(
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "vendedor@empresa.com")], "Teste", ClaimTypes.Name, ClaimTypes.Role)))));

        var excecao = Assert.Throws<InvalidOperationException>(() => tenant.EmpresaId);

        Assert.Contains("sem empresa", excecao.Message);
        Assert.Contains("super admin", excecao.Message);
    }

    [Fact]
    public void ProviderIndisponivelForaDeCircuito_DeveCairParaCentral_SemLancar()
    {
        // Condição CONHECIDA do ServerAuthenticationStateProvider sem circuito/escopo Razor
        // (seed na inicialização, SSR estático): deve tratar como central e manter o seed vivo.
        var tenant = new TenantContext(new FakeAuthStateProvider(
            () => throw new InvalidOperationException(
                "Do not call GetAuthenticationStateAsync outside of the DI scope for a Razor component. "
                + "Typically, this means you can call it only within a Razor component or inside another "
                + "DI service that is resolved for a Razor component.")));

        Assert.Null(tenant.EmpresaId);
        Assert.Equal(string.Empty, tenant.NomeEmpresa);
        Assert.False(tenant.EhSuperAdmin);
    }

    [Fact]
    public void ExcecaoInesperadaDoProvider_DevePropagar_NaoDegradarParaCentral()
    {
        // Qualquer exceção que NÃO seja a condição fora-de-circuito deve propagar (fail loud),
        // nunca virar acesso a todos os tenants.
        var tenant = new TenantContext(new FakeAuthStateProvider(
            () => throw new InvalidOperationException("Falha inesperada ao obter o estado de autenticação.")));

        var excecao = Assert.Throws<InvalidOperationException>(() => tenant.EmpresaId);

        Assert.Contains("Falha inesperada", excecao.Message);
    }

    /// <summary>Provider configurável: devolve um estado de autenticação ou lança, conforme o cenário.</summary>
    private sealed class FakeAuthStateProvider(Func<AuthenticationState> obter) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(obter());
    }
}