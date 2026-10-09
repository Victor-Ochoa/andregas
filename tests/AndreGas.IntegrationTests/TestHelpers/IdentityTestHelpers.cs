using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AndreGas.IntegrationTests.TestHelpers;

/// <summary>
/// Monta o <c>UserManager</c>/<c>RoleManager</c> do Identity sobre o Postgres real de forma fiel à
/// produção (via <c>AddIdentity</c> + <c>AddEntityFrameworkStores</c>), evitando construir o
/// <c>UserManager</c> manualmente (que exige muitos serviços internos e é frágil).
/// </summary>
public static class IdentityTestHelpers
{
    public static (UserManager<ApplicationUser> UserManager, RoleManager<ApplicationRole> RoleManager, ServiceProvider Provider)
        CriarManagers(string connectionString)
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        services
            .AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        var provider = services.BuildServiceProvider();

        return (
            provider.GetRequiredService<UserManager<ApplicationUser>>(),
            provider.GetRequiredService<RoleManager<ApplicationRole>>(),
            provider);
    }
}