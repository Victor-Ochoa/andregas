using Microsoft.AspNetCore.Identity;

using RevendaApp.Infrastructure.Identity;
using RevendaApp.Web.Features.Auth;

namespace RevendaApp.Web.Features.Usuarios.Cadastrar;

/// <summary>
/// Implementação de <see cref="IUsuarioWriter"/> baseada no Identity. Cria o usuário
/// (<see cref="UserManager{TUser}.CreateAsync(TUser, string)"/>) e o atribui à role via
/// <see cref="UserManager{TUser}.AddToRoleAsync"/>. Garante que as roles existam antes
/// (reutiliza <see cref="SeedData.GarantirRolesAsync"/>).
/// </summary>
public sealed class IdentityUsuarioWriter(UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager)
    : IUsuarioWriter
{
    public async Task<bool> EmailJaUsadoAsync(string email, CancellationToken cancellationToken = default)
        => await userManager.FindByEmailAsync(email) is not null;

    public async Task<(Guid? UsuarioId, string? Erro)> CriarComRoleAsync(
        string nome, string email, string papel, string senha, Guid? empresaId,
        CancellationToken cancellationToken = default)
    {
        // Garante que as roles existam (ex.: primeiro acesso quando o seed não rodou).
        await SeedData.GarantirRolesAsync(roleManager);

        var usuario = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            Nome = nome,
            // Identity não é entidade de domínio: o EmpresaId é atribuído explicitamente aqui
            // (o SetEmpresaIdInterceptor só cobre as 10 entidades de domínio).
            EmpresaId = empresaId,
        };

        var createResult = await userManager.CreateAsync(usuario, senha);
        if (!createResult.Succeeded)
        {
            return (null, string.Join("; ", createResult.Errors.Select(e => e.Description)));
        }

        var roleResult = await userManager.AddToRoleAsync(usuario, papel);
        if (!roleResult.Succeeded)
        {
            return (null, string.Join("; ", roleResult.Errors.Select(e => e.Description)));
        }

        return (usuario.Id, null);
    }
}