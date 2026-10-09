using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Identity;
using AndreGas.IntegrationTests.TestHelpers;
using AndreGas.Web.Features.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AndreGas.IntegrationTests.Tests;

/// <summary>
/// Testes do seed do admin (<see cref="SeedData.SeedDefaultAdminUserAsync"/>), focados no cenário de
/// produção: banco já com admin existente mas roles ausentes (ex.: deploy em cima de banco com admin
/// criado por execução anterior) deve garantir que as roles <see cref="ApplicationRole.Admin"/> e
/// <see cref="ApplicationRole.Vendedor"/> sejam criadas.
/// </summary>
public class SeedDataFeatureTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SeedDefaultAdminUser_QuandoAdminJaExiste_SemRoles_DeveCriarRoles()
    {
        using var db = fixture.CreateDbContext();

        // Monta os managers do Identity fiel à produção sobre o Postgres real.
        var (userManager, roleManager, provider) = IdentityTestHelpers.CriarManagers(fixture.ConnectionString);

        // Garante o estado exato do cenário: remove quaisquer roles e o admin que possam sobrar
        // de execuções anteriores (o volume do Postgres persiste entre execuções da fixture).
        await LimparIdentityAsync(db);

        // Pré-existente: um admin no banco, mas SEM as roles (cenário de produção: banco com admin
        // criado por execução anterior, onde o seed retornava cedo sem garantir roles).
        var adminExistente = new ApplicationUser
        {
            UserName = "admin@andregas.com.br",
            Email = "admin@andregas.com.br",
            EmailConfirmed = true,
            Nome = "Administrador",
        };
        await userManager.CreateAsync(adminExistente, "AndreGas@123");
        Assert.False(await roleManager.RoleExistsAsync(ApplicationRole.Admin));
        Assert.False(await roleManager.RoleExistsAsync(ApplicationRole.Vendedor));

        // Executa o seed como o startup faria (admin já existe -> retorna cedo).
        var configuration = new ConfigurationBuilder().Build();
        await SeedData.SeedDefaultAdminUserAsync(provider, configuration);

        // As roles DEVEM ter sido criadas mesmo com o admin já existente.
        Assert.True(await roleManager.RoleExistsAsync(ApplicationRole.Admin));
        Assert.True(await roleManager.RoleExistsAsync(ApplicationRole.Vendedor));

        provider.Dispose();
    }

    private static async Task LimparIdentityAsync(AppDbContext db)
    {
        db.UserRoles.RemoveRange(db.UserRoles);
        db.Roles.RemoveRange(db.Roles);
        db.Users.RemoveRange(db.Users);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task SeedDefaultAdminUser_QuandoAdminNaoExiste_DeveCriarAdminComRoleAdmin()
    {
        using var db = fixture.CreateDbContext();

        var (userManager, roleManager, provider) = IdentityTestHelpers.CriarManagers(fixture.ConnectionString);
        await LimparIdentityAsync(db);

        var configuration = new ConfigurationBuilder().Build();
        await SeedData.SeedDefaultAdminUserAsync(provider, configuration);

        Assert.True(await roleManager.RoleExistsAsync(ApplicationRole.Admin));
        Assert.True(await roleManager.RoleExistsAsync(ApplicationRole.Vendedor));

        var admin = await userManager.FindByEmailAsync(SeedData.DefaultEmail);
        Assert.NotNull(admin);
        Assert.True(await userManager.IsInRoleAsync(admin!, ApplicationRole.Admin));

        provider.Dispose();
    }
}