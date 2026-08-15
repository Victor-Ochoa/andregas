using AndreGas.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace AndreGas.Web.Features.Auth;

/// <summary>
/// Garante que sempre exista um usuário administrador para acessar o sistema. As credenciais
/// podem ser sobrescritas via configuração (chaves "SeedAdmin:Email" e "SeedAdmin:Password");
/// caso contrário, usa um valor padrão adequado apenas para desenvolvimento.
/// </summary>
public static class SeedData
{
    public const string DefaultEmail = "admin@andregas.com.br";
    public const string DefaultPassword = "AndreGas@123";

    public static async Task SeedDefaultAdminUserAsync(IServiceProvider services, IConfiguration configuration)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var email = configuration["SeedAdmin:Email"] ?? DefaultEmail;
        var password = configuration["SeedAdmin:Password"] ?? DefaultPassword;

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Falha ao criar o usuário administrador padrão: {errors}");
        }
    }
}
