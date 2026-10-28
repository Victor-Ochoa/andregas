using AndreGas.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity;

namespace AndreGas.Web.Features.Auth;

/// <summary>
/// Garante que sempre exista um <b>super administrador da central</b> para o primeiro acesso ao
/// sistema. As credenciais podem ser sobrescritas via configuração (chaves "SeedAdmin:Email" e
/// "SeedAdmin:Password"); caso contrário, usa um valor padrão adequado apenas para desenvolvimento.
/// </summary>
/// <remarks>
/// <para>
/// Multi-empresa: o bootstrap cria apenas o super admin <b>sem empresa</b> (<c>EmpresaId = null</c>)
/// na role <see cref="ApplicationRole.SuperAdmin"/>. Empresas e seus administradores são criados
/// depois, pela central — o bootstrap nunca cria empresa nem admin de empresa.
/// </para>
/// </remarks>
public static class SeedData
{
    public const string DefaultEmail = "admin@andregas.com.br";
    public const string DefaultPassword = "AndreGas@123";

    /// <summary>Nome de exibição do super admin criado no bootstrap.</summary>
    public const string NomeSuperAdmin = "Super Administrador";

    public static async Task SeedDefaultAdminUserAsync(IServiceProvider services, IConfiguration configuration,
        bool requireExplicitCredentials = false)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

        var email = configuration["SeedAdmin:Email"];
        var password = configuration["SeedAdmin:Password"];

        if (requireExplicitCredentials)
        {
            // Em Produção não há fallback para o default de dev: o super admin DEVE ter sido
            // configurado via ambiente (SeedAdmin:Email / SeedAdmin:Password). Falha rápido em vez
            // de subir com credenciais conhecidas e fracas.
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

        // Garante que as roles existam (criando-as se não existirem) ANTES de qualquer verificação.
        // Sem isso, num banco onde o usuário já foi criado por execução anterior (comum em
        // produção), o seed retornava cedo e as roles nunca eram criadas.
        await GarantirRolesAsync(roleManager);

        var usuario = await userManager.FindByEmailAsync(email);
        if (usuario is null)
        {
            usuario = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                Nome = NomeSuperAdmin,
                // Super admin da central: sem empresa e ativo. Explícito (não depende do default
                // do banco/propriedade) para não criar usuário inativo por engano.
                EmpresaId = null,
                Ativo = true,
            };

            var result = await userManager.CreateAsync(usuario, password);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Falha ao criar o usuário super administrador padrão: {errors}");
            }
        }

        // Garante o vínculo do super admin à role SuperAdmin mesmo quando o usuário já existia
        // (cenário de banco antigo em que o seed parava antes de AddToRoleAsync). Idempotente.
        if (!await userManager.IsInRoleAsync(usuario, ApplicationRole.SuperAdmin))
        {
            await userManager.AddToRoleAsync(usuario, ApplicationRole.SuperAdmin);
        }
    }

    /// <summary>Cria as roles <see cref="ApplicationRole.Admin"/>, <see cref="ApplicationRole.Vendedor"/>
    /// e <see cref="ApplicationRole.SuperAdmin"/> se ainda não existirem. Idempotente — chamado no
    /// seed e também quando um admin cadastra usuários.</summary>
    public static async Task GarantirRolesAsync(RoleManager<ApplicationRole> roleManager)
    {
        foreach (var roleName in new[] { ApplicationRole.Admin, ApplicationRole.Vendedor, ApplicationRole.SuperAdmin })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new ApplicationRole(roleName));
            }
        }
    }
}