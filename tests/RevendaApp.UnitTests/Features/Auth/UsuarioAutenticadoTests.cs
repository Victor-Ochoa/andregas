using System.Security.Claims;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using RevendaApp.Infrastructure.Identity;
using RevendaApp.UnitTests.TestHelpers;
using RevendaApp.Web.Common.Authorization;

namespace RevendaApp.UnitTests.Features.Auth;

public class UsuarioAutenticadoTests
{
    [Fact]
    public async Task EhAdministradorAsync_DeveRetornarTrue_QuandoPrincipalTemClaimAdmin()
    {
        using var provider = IdentityTestHelpers.CriarIdentityInMemory();
        var auth = new FakeAuthenticationStateProvider(new Claim(ClaimTypes.Role, "Admin"));
        var sut = new UsuarioAutenticado(auth, provider.GetRequiredService<UserManager<ApplicationUser>>());

        // Caminho rápido (claim de role) — nem consulta o banco.
        Assert.True(await sut.EhAdministradorAsync());
    }

    [Fact]
    public async Task EhAdministradorAsync_DeveRetornarTrue_QuandoSemClaimMasRoleNoBanco()
    {
        using var provider = IdentityTestHelpers.CriarIdentityInMemory();
        await IdentityTestHelpers.CriarUsuarioAdminAsync(provider); // admin@revenda.app.com.br

        // Principal autenticado SEM claim de role (ex.: role atribuída sem atualizar o principal).
        var auth = new FakeAuthenticationStateProvider(new Claim(ClaimTypes.Name, "admin@revenda.app.com.br"));
        var sut = new UsuarioAutenticado(auth, provider.GetRequiredService<UserManager<ApplicationUser>>());

        Assert.True(await sut.EhAdministradorAsync());
    }

    [Fact]
    public async Task EhAdministradorAsync_DeveRetornarFalse_QuandoSemClaimEUsuarioNaoAdmin()
    {
        using var provider = IdentityTestHelpers.CriarIdentityInMemory();
        await IdentityTestHelpers.CriarUsuarioAsync(provider, "vendedor@revenda.app.com.br", "Vendedor");

        var auth = new FakeAuthenticationStateProvider(new Claim(ClaimTypes.Name, "vendedor@revenda.app.com.br"));
        var sut = new UsuarioAutenticado(auth, provider.GetRequiredService<UserManager<ApplicationUser>>());

        Assert.False(await sut.EhAdministradorAsync());
    }

    [Fact]
    public async Task EhAdministradorAsync_DeveRetornarFalse_QuandoUsuarioNaoExiste()
    {
        using var provider = IdentityTestHelpers.CriarIdentityInMemory();

        // Principal autenticado com e-mail removido do banco: não pode ser admin.
        var auth = new FakeAuthenticationStateProvider(new Claim(ClaimTypes.Name, "ninguem@revenda.app.com.br"));
        var sut = new UsuarioAutenticado(auth, provider.GetRequiredService<UserManager<ApplicationUser>>());

        Assert.False(await sut.EhAdministradorAsync());
    }

    [Fact]
    public async Task EhAdministradorAsync_DeveRetornarFalse_QuandoAnonimo()
    {
        using var provider = IdentityTestHelpers.CriarIdentityInMemory();

        // Principal com claim de role, PORÉM anônimo (identity sem authenticationType): a guarda
        // do UsuarioAutenticado não pode confiar em claims de principal anônimo.
        var auth = new FakeAuthenticationStateProvider(
            new[] { new Claim(ClaimTypes.Role, "Admin") }, autenticado: false);
        var principal = await auth.GetAuthenticationStateAsync();
        Assert.False(principal.User.Identity?.IsAuthenticated);

        var sut = new UsuarioAutenticado(auth, provider.GetRequiredService<UserManager<ApplicationUser>>());

        Assert.False(await sut.EhAdministradorAsync());
    }

    [Fact]
    public async Task EhAdministradorAsync_DeveRetornarFalse_QuandoPrincipalVazio()
    {
        using var provider = IdentityTestHelpers.CriarIdentityInMemory();
        var auth = new FakeAuthenticationStateProvider(); // sem claims → anônimo
        var sut = new UsuarioAutenticado(auth, provider.GetRequiredService<UserManager<ApplicationUser>>());

        Assert.False(await sut.EhAdministradorAsync());
    }

    [Fact]
    public async Task RequerAdminAsync_DeveLancar_QuandoUsuarioNaoAdmin()
    {
        using var provider = IdentityTestHelpers.CriarIdentityInMemory();
        var auth = new FakeAuthenticationStateProvider(new Claim(ClaimTypes.Name, "vendedor@revenda.app.com.br"));
        var sut = new UsuarioAutenticado(auth, provider.GetRequiredService<UserManager<ApplicationUser>>());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RequerAdminAsync());

        Assert.Contains("Somente administradores", ex.Message);
    }

    [Fact]
    public async Task RequerAdminAsync_DevePassar_QuandoUsuarioAdmin()
    {
        using var provider = IdentityTestHelpers.CriarIdentityInMemory();
        await IdentityTestHelpers.CriarUsuarioAdminAsync(provider);

        var auth = new FakeAuthenticationStateProvider(new Claim(ClaimTypes.Name, "admin@revenda.app.com.br"));
        var sut = new UsuarioAutenticado(auth, provider.GetRequiredService<UserManager<ApplicationUser>>());

        await sut.RequerAdminAsync(); // não lança
    }
}