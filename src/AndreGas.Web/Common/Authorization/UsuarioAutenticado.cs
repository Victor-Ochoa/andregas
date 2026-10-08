using AndreGas.Infrastructure.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace AndreGas.Web.Common.Authorization;

/// <summary>
/// Implementação scoped de <see cref="IUsuarioAutenticado"/> que resolve o usuário atual da
/// <see cref="AuthenticationStateProvider"/> do circuito Blazor. A checagem usa a role claim do
/// principal; quando não há claim (ex.: usuário criado em outra role) ou para reforço, consulta o
/// <see cref="UserManager{TUser}"/>.
/// </summary>
public sealed class UsuarioAutenticado(AuthenticationStateProvider authStateProvider, UserManager<ApplicationUser> userManager)
    : IUsuarioAutenticado
{
    public async Task<bool> EhAdministradorAsync(CancellationToken cancellationToken = default)
    {
        var state = await authStateProvider.GetAuthenticationStateAsync();
        var user = state.User;

        if (user.Identity is not { IsAuthenticated: true })
        {
            return false;
        }

        // Claim de role via Identity (default claim type) é o caminho rápido; se presente e
        // verdadeiro, resolve sem consulta ao banco. Reforço via UserManager caso o claim esteja
        // ausente (ex.: a role foi atribuída sem atualizar o principal).
        if (user.IsInRole(ApplicationRole.Admin))
        {
            return true;
        }

        var appUser = await userManager.FindByNameAsync(user.Identity.Name!);
        return appUser is not null && await userManager.IsInRoleAsync(appUser, ApplicationRole.Admin);
    }

    public async Task RequerAdminAsync(CancellationToken cancellationToken = default)
    {
        if (!await EhAdministradorAsync(cancellationToken))
        {
            throw new InvalidOperationException("Somente administradores podem executar esta operação.");
        }
    }
}