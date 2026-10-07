using AndreGas.Infrastructure;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Common;

/// <summary>
/// Helper para obter o usuário autenticado atual a partir do <see cref="AuthenticationStateProvider"/>
/// scoped do circuito Blazor. Quando o provider é null (ou o usuário não está autenticado), retorna
/// "sistema" (nome) e Id nulo — usados pelos registros de auditoria (histórico de estoque) e pelo
/// vendedor da venda.
/// </summary>
public static class UsuarioAtual
{
    public const string Sistema = "sistema";

    public static async Task<(string Nome, Guid? Id)> ObterAsync(
        AuthenticationStateProvider? authStateProvider,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        if (authStateProvider is null)
        {
            return (Sistema, null);
        }

        var state = await authStateProvider.GetAuthenticationStateAsync();
        var identity = state.User.Identity;

        if (identity is not { IsAuthenticated: true })
        {
            return (Sistema, null);
        }

        var nome = identity.Name ?? Sistema;

        var userId = await db.Users
            .Where(u => u.Email == nome)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return (nome, userId);
    }
}