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

    public static async Task SeedDefaultAdminUserAsync(IServiceProvider services, IConfiguration configuration,
        bool requireExplicitCredentials = false)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

        var email = configuration["SeedAdmin:Email"];
        var password = configuration["SeedAdmin:Password"];

        if (requireExplicitCredentials)
        {
            // Em Produção não há fallback para o default de dev: o admin DEVE ter sido configurado
            // via ambiente (SeedAdmin:Email / SeedAdmin:Password). Falha rápido em vez de subir com
            // credenciais conhecidas e fracas.
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "Credenciais do admin não configuradas em Produção. Defina SeedAdmin:Email e SeedAdmin:Password via variáveis de ambiente.");
            }
        }
        else
        {
            email ??= DefaultEmail;
            password ??= DefaultPassword;
        }

        // Garante que os dois papéis existam (criando-os se não existirem) ANTES de qualquer
        // verificação. Sem isso, num banco onde o admin já foi criado por execução anterior
        // (comum em produção), o seed retornava cedo e as roles nunca eram criadas.
        await GarantirRolesAsync(roleManager);

        var usuario = await userManager.FindByEmailAsync(email);
        if (usuario is null)
        {
            usuario = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                Nome = "Administrador",
            };

            var result = await userManager.CreateAsync(usuario, password);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Falha ao criar o usuário administrador padrão: {errors}");
            }
        }

        // Garante o vínculo do admin à role Admin mesmo quando o usuário já existia (cenário de
        // banco antigo em que o seed parava antes de AddToRoleAsync). Idempotente.
        if (!await userManager.IsInRoleAsync(usuario, ApplicationRole.Admin))
        {
            await userManager.AddToRoleAsync(usuario, ApplicationRole.Admin);
        }
    }

    /// <summary>Cria as roles <see cref="ApplicationRole.Admin"/> e <see cref="ApplicationRole.Vendedor"/>
    /// se ainda não existirem. Idempotente — chamado no seed e também quando um admin cadastra
    /// usuários.</summary>
    public static async Task GarantirRolesAsync(RoleManager<ApplicationRole> roleManager)
    {
        foreach (var roleName in new[] { ApplicationRole.Admin, ApplicationRole.Vendedor })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new ApplicationRole(roleName));
            }
        }
    }
}