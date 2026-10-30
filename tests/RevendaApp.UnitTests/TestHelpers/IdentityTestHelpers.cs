using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using RevendaApp.Infrastructure;
using RevendaApp.Infrastructure.Identity;
using RevendaApp.Web.Features.Auth;

namespace RevendaApp.UnitTests.TestHelpers;

/// <summary>
/// Monta um <see cref="ServiceProvider"/> com o Identity completo (UserManager, RoleManager) sobre
/// o mesmo <see cref="AppDbContext"/> InMemory usado nos demais testes — permite exercitar
/// <see cref="SeedData"/> e <see cref="RevendaApp.Web.Common.Authorization.UsuarioAutenticado"/> com o
/// fluxo real do Identity, sem Postgres e sem mocks.
/// </summary>
public static class IdentityTestHelpers
{
    public static ServiceProvider CriarIdentityInMemory()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(InMemoryDbContextFactory.Create());
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>();
        return services.BuildServiceProvider();
    }

    /// <summary>Cria um usuário na role Admin (usando o fluxo real do Identity + roles via
    /// <see cref="SeedData.GarantirRolesAsync"/>), retornando o usuário criado.</summary>
    public static async Task<ApplicationUser> CriarUsuarioAdminAsync(ServiceProvider provider, string email = "admin@revenda.app.com.br")
        => await CriarUsuarioAsync(provider, email, ApplicationRole.Admin);

    /// <summary>Cria um usuário na role informada (usando o fluxo real do Identity).</summary>
    public static async Task<ApplicationUser> CriarUsuarioAsync(ServiceProvider provider, string email, string role)
    {
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = provider.GetRequiredService<RoleManager<ApplicationRole>>();
        await SeedData.GarantirRolesAsync(roleManager);

        var usuario = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            Nome = email.Split('@')[0],
        };

        var result = await userManager.CreateAsync(usuario, "RevendaApp@123");
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Falha ao criar usuário no teste: " +
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRoleAsync(usuario, role);
        return usuario;
    }
}