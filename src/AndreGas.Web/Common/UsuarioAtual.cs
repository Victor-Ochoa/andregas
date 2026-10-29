using System.Security.Claims;

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

        // O Id vem do NameIdentifier da identidade ASSINADA (o Identity sempre o emite no cookie).
        // Antes o Id era resolvido por uma reconsulta `Users.Where(u => u.Email == nome)` — uma
        // busca na tabela do Identity, que NÃO tem filtro de empresa. Aquela forma só está correta
        // enquanto `RequireUniqueEmail = true` valer (garantia distante deste ponto) e devolve null
        // se o e-mail do principal divergir do gravado (ex.: e-mail alterado sem novo login).
        var userId = ObterIdDoPrincipal(state.User);

        if (userId is null)
        {
            // Fallback: principals montados sem NameIdentifier (chamadas internas e testes que só
            // informam ClaimTypes.Name) continuam resolvendo o Id pelo e-mail, como antes.
            userId = await db.Users
                .Where(u => u.Email == nome)
                .Select(u => (Guid?)u.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return (nome, userId);
    }

    /// <summary>Id do usuário a partir do <c>NameIdentifier</c>; <c>null</c> se ausente ou inválido.</summary>
    private static Guid? ObterIdDoPrincipal(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) && id != Guid.Empty
            ? id
            : null;
}