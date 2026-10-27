using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Identity;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Auth;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AndreGas.UnitTests.Features.Auth;

public class SeedDataTests
{
    [Fact]
    public async Task SeedDefaultAdminUserAsync_RequireExplicitCredentials_SemConfiguracao_DeveLancar()
    {
        using var provider = IdentityTestHelpers.CriarIdentityInMemory();
        var config = new ConfigurationBuilder().Build(); // vazio

        // Em produção (requireExplicitCredentials=true) sem SeedAdmin:Email/Password não pode haver
        // fallback para credenciais conhecidas de dev — falha rápido.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SeedData.SeedDefaultAdminUserAsync(provider, config, requireExplicitCredentials: true));
    }

    [Fact]
    public async Task SeedDefaultAdminUserAsync_RequireExplicitCredentials_ComConfiguracao_DeveCriarAdmin()
    {
        using var provider = IdentityTestHelpers.CriarIdentityInMemory();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SeedAdmin:Email"] = "dono@andregas.com.br",
                ["SeedAdmin:Password"] = "Senh4!Forte",
            })
            .Build();

        await SeedData.SeedDefaultAdminUserAsync(provider, config, requireExplicitCredentials: true);

        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await userManager.FindByEmailAsync("dono@andregas.com.br");
        Assert.NotNull(admin);
        Assert.True(await userManager.IsInRoleAsync(admin!, "Admin"));
    }

    [Fact]
    public async Task SeedDefaultAdminUserAsync_RequireExplicitCredentials_SoEmailSemSenha_DeveLancar()
    {
        using var provider = IdentityTestHelpers.CriarIdentityInMemory();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SeedAdmin:Email"] = "dono@andregas.com.br",
                ["SeedAdmin:Password"] = "   ", // em branco
            })
            .Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SeedData.SeedDefaultAdminUserAsync(provider, config, requireExplicitCredentials: true));
    }

    [Fact]
    public async Task SeedDefaultAdminUserAsync_ModoDesenvolvimento_SemConfiguracao_DeveUsarCredenciaisPadrao()
    {
        using var provider = IdentityTestHelpers.CriarIdentityInMemory();
        var config = new ConfigurationBuilder().Build();

        await SeedData.SeedDefaultAdminUserAsync(provider, config, requireExplicitCredentials: false);

        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await userManager.FindByEmailAsync(SeedData.DefaultEmail);
        Assert.NotNull(admin);
        Assert.True(await userManager.IsInRoleAsync(admin!, "Admin"));
    }

    [Fact]
    public async Task SeedDefaultAdminUserAsync_QuandoAdminJaExiste_DeveReatribuirRoleAdmin_Idempotente()
    {
        using var provider = IdentityTestHelpers.CriarIdentityInMemory();
        var config = new ConfigurationBuilder().Build();

        // 1ª execução cria.
        await SeedData.SeedDefaultAdminUserAsync(provider, config);

        // Remove a role para simular banco legado sem vínculo Admin.
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await userManager.FindByEmailAsync(SeedData.DefaultEmail);
        await userManager.RemoveFromRoleAsync(admin!, "Admin");

        // 2ª execução deve reatribuir.
        await SeedData.SeedDefaultAdminUserAsync(provider, config);

        Assert.True(await userManager.IsInRoleAsync(admin!, "Admin"));
    }

    [Fact]
    public async Task GarantirRolesAsync_DeveCriarAmbasRoles_QuandoNaoExistem()
    {
        using var provider = IdentityTestHelpers.CriarIdentityInMemory();
        var roleManager = provider.GetRequiredService<RoleManager<ApplicationRole>>();

        await SeedData.GarantirRolesAsync(roleManager);

        Assert.True(await roleManager.RoleExistsAsync("Admin"));
        Assert.True(await roleManager.RoleExistsAsync("Vendedor"));
    }

    [Fact]
    public async Task GarantirRolesAsync_DeveSerIdempotente_NaoDuplicarRoles()
    {
        using var provider = IdentityTestHelpers.CriarIdentityInMemory();
        var roleManager = provider.GetRequiredService<RoleManager<ApplicationRole>>();

        await SeedData.GarantirRolesAsync(roleManager);
        await SeedData.GarantirRolesAsync(roleManager);

        Assert.Equal(2, provider.GetRequiredService<AppDbContext>().Roles.Count());
    }
}