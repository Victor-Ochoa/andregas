using AndreGas.Domain.Entities;
using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Identity;
using AndreGas.IntegrationTests.TestHelpers;
using AndreGas.Web.Features.Auth;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AndreGas.IntegrationTests.Tests;

/// <summary>
/// Testes do seed do bootstrap (<see cref="SeedData.SeedDefaultAdminUserAsync"/>). Multi-empresa:
/// o usuário criado no bootstrap é o <b>super admin da central</b> — sem <c>EmpresaId</c>, na role
/// <see cref="ApplicationRole.SuperAdmin"/> — e não um admin de empresa. Também cobre o cenário de
/// produção (banco já com o usuário, mas roles ausentes) e a garantia de que a role SuperAdmin
/// exista.
/// </summary>
public class SeedDataFeatureTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Seed_cria_superadmin_da_central()
    {
        using var db = fixture.CreateDbContext();

        var (userManager, roleManager, provider) = IdentityTestHelpers.CriarManagers(fixture.ConnectionString);
        await LimparIdentityAsync(db);

        var configuration = new ConfigurationBuilder().Build();
        await SeedData.SeedDefaultAdminUserAsync(provider, configuration);

        // A role SuperAdmin existe (junto com Admin e Vendedor).
        Assert.True(await roleManager.RoleExistsAsync(ApplicationRole.SuperAdmin));
        Assert.True(await roleManager.RoleExistsAsync(ApplicationRole.Admin));
        Assert.True(await roleManager.RoleExistsAsync(ApplicationRole.Vendedor));

        var superAdmin = await userManager.FindByEmailAsync(SeedData.DefaultEmail);
        Assert.NotNull(superAdmin);

        // Sem empresa (central), ativo, nome de exibição e role SuperAdmin — e NÃO admin de empresa.
        Assert.Null(superAdmin!.EmpresaId);
        Assert.True(superAdmin.Ativo);
        Assert.Equal("Super Administrador", superAdmin.Nome);
        Assert.True(await userManager.IsInRoleAsync(superAdmin, ApplicationRole.SuperAdmin));
        Assert.False(await userManager.IsInRoleAsync(superAdmin, ApplicationRole.Admin));

        provider.Dispose();
    }

    [Fact]
    public async Task SeedDefaultAdminUser_QuandoUsuarioJaExiste_SemRoles_DeveCriarRoles()
    {
        using var db = fixture.CreateDbContext();

        // Monta os managers do Identity fiel à produção sobre o Postgres real.
        var (userManager, roleManager, provider) = IdentityTestHelpers.CriarManagers(fixture.ConnectionString);

        // Garante o estado exato do cenário: remove quaisquer roles e o admin que possam sobrar
        // de execuções anteriores (o volume do Postgres persiste entre execuções da fixture).
        await LimparIdentityAsync(db);

        // Pré-existente: um usuário no banco, mas SEM as roles (cenário de produção: banco com
        // admin criado por execução anterior, onde o seed retornava cedo sem garantir roles).
        var usuarioExistente = new ApplicationUser
        {
            UserName = "admin@andregas.com.br",
            Email = "admin@andregas.com.br",
            EmailConfirmed = true,
            Nome = "Administrador",
        };
        await userManager.CreateAsync(usuarioExistente, "AndreGas@123");
        Assert.False(await roleManager.RoleExistsAsync(ApplicationRole.Admin));
        Assert.False(await roleManager.RoleExistsAsync(ApplicationRole.Vendedor));
        Assert.False(await roleManager.RoleExistsAsync(ApplicationRole.SuperAdmin));

        // Executa o seed como o startup faria (usuário já existe -> não recria).
        var configuration = new ConfigurationBuilder().Build();
        await SeedData.SeedDefaultAdminUserAsync(provider, configuration);

        // As roles DEVEM ter sido criadas mesmo com o usuário já existente.
        Assert.True(await roleManager.RoleExistsAsync(ApplicationRole.Admin));
        Assert.True(await roleManager.RoleExistsAsync(ApplicationRole.Vendedor));
        Assert.True(await roleManager.RoleExistsAsync(ApplicationRole.SuperAdmin));

        provider.Dispose();
    }

    [Fact]
    public async Task SeedDefaultAdminUser_QuandoUsuarioJaExiste_SemRoleSuperAdmin_DeveVincularSuperAdmin()
    {
        using var db = fixture.CreateDbContext();

        var (userManager, roleManager, provider) = IdentityTestHelpers.CriarManagers(fixture.ConnectionString);
        await LimparIdentityAsync(db);

        // Cenário de produção: as roles existem, mas o usuário criado por execução anterior acabou
        // sem o vínculo à role SuperAdmin (o seed antigo parava antes de AddToRoleAsync).
        await roleManager.CreateAsync(new ApplicationRole(ApplicationRole.Admin));
        await roleManager.CreateAsync(new ApplicationRole(ApplicationRole.Vendedor));
        await roleManager.CreateAsync(new ApplicationRole(ApplicationRole.SuperAdmin));

        var usuarioExistente = new ApplicationUser
        {
            UserName = "admin@andregas.com.br",
            Email = "admin@andregas.com.br",
            EmailConfirmed = true,
            Nome = "Administrador",
        };
        await userManager.CreateAsync(usuarioExistente, "AndreGas@123");
        Assert.False(await userManager.IsInRoleAsync(usuarioExistente, ApplicationRole.SuperAdmin));

        // Executa o seed como o startup faria (usuário já existe -> retorna cedo no create).
        var configuration = new ConfigurationBuilder().Build();
        await SeedData.SeedDefaultAdminUserAsync(provider, configuration);

        // O usuário existente DEVE passar a ter a role SuperAdmin.
        Assert.True(await userManager.IsInRoleAsync(usuarioExistente, ApplicationRole.SuperAdmin));

        provider.Dispose();
    }

    [Fact]
    public async Task SeedDefaultAdminUser_QuandoUsuarioJaExisteComRoleAdminLegada_DeveFicarSomenteSuperAdmin()
    {
        using var db = fixture.CreateDbContext();

        var (userManager, roleManager, provider) = IdentityTestHelpers.CriarManagers(fixture.ConnectionString);
        await LimparIdentityAsync(db);

        await roleManager.CreateAsync(new ApplicationRole(ApplicationRole.Admin));
        await roleManager.CreateAsync(new ApplicationRole(ApplicationRole.Vendedor));
        await roleManager.CreateAsync(new ApplicationRole(ApplicationRole.SuperAdmin));

        // Usuário legado do seed antigo: era "Admin" da central, com o nome antigo.
        var usuarioLegado = new ApplicationUser
        {
            UserName = "admin@andregas.com.br",
            Email = "admin@andregas.com.br",
            EmailConfirmed = true,
            Nome = "Administrador",
            EmpresaId = null,
        };
        await userManager.CreateAsync(usuarioLegado, "AndreGas@123");
        await userManager.AddToRoleAsync(usuarioLegado, ApplicationRole.Admin);

        var configuration = new ConfigurationBuilder().Build();
        await SeedData.SeedDefaultAdminUserAsync(provider, configuration);

        // SuperAdmin da central NÃO pode acumular a role Admin: com Admin ele passaria por
        // Politicas.ApenasAdmin/RequerAdminAsync enquanto o TenantContext está em modo central
        // (EmpresaId nulo), lendo e gravando dados de todas as empresas.
        Assert.True(await userManager.IsInRoleAsync(usuarioLegado, ApplicationRole.SuperAdmin));
        Assert.False(await userManager.IsInRoleAsync(usuarioLegado, ApplicationRole.Admin));

        // E o nome de exibição é corrigido para o do super admin da central.
        var recarregado = await userManager.FindByEmailAsync(usuarioLegado.Email!);
        Assert.Equal(SeedData.NomeSuperAdmin, recarregado!.Nome);

        provider.Dispose();
    }

    [Fact]
    public async Task SeedDefaultAdminUser_QuandoUsuarioDoSeedPertenceAEmpresa_DeveLancar()
    {
        using var db = fixture.CreateDbContext();
        await LimparIdentityAsync(db);

        // Empresa qualquer: o usuário do seed é da central e não pode ter EmpresaId (senão
        // viraria um SuperAdmin com escopo de empresa). Criada DEPOIS da limpeza, que apaga Empresas.
        var empresa = new Empresa("empresa-seed-invalido", "Empresa do Seed Inválido");
        db.Set<Empresa>().Add(empresa);
        await db.SaveChangesAsync();

        var (userManager, roleManager, provider) = IdentityTestHelpers.CriarManagers(fixture.ConnectionString);

        var usuarioComEmpresa = new ApplicationUser
        {
            UserName = "admin@andregas.com.br",
            Email = "admin@andregas.com.br",
            EmailConfirmed = true,
            Nome = "Administrador",
            EmpresaId = empresa.Id,
        };
        await userManager.CreateAsync(usuarioComEmpresa, "AndreGas@123");

        var configuration = new ConfigurationBuilder().Build();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SeedData.SeedDefaultAdminUserAsync(provider, configuration));

        Assert.Contains("super admin da central", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("empresa", ex.Message, StringComparison.OrdinalIgnoreCase);

        provider.Dispose();
    }

    [Fact]
    public async Task SeedDefaultAdminUser_EmProducao_SemCredenciaisExplicitas_DeveFalhar()
    {
        using var db = fixture.CreateDbContext();

        var (_, _, provider) = IdentityTestHelpers.CriarManagers(fixture.ConnectionString);
        await LimparIdentityAsync(db);

        var configuration = new ConfigurationBuilder().Build();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SeedData.SeedDefaultAdminUserAsync(provider, configuration, requireExplicitCredentials: true));

        provider.Dispose();
    }

    private static async Task LimparIdentityAsync(AppDbContext db)
    {
        db.UserRoles.RemoveRange(db.UserRoles);
        db.Roles.RemoveRange(db.Roles);
        db.Users.RemoveRange(db.Users);
        await db.SaveChangesAsync();
    }
}