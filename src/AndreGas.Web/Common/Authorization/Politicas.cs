using AndreGas.Infrastructure.Identity;

using Microsoft.AspNetCore.Authorization;

namespace AndreGas.Web.Common.Authorization;

/// <summary>
/// Políticas de autorização declarativas usadas nas rotas Blazor ([Authorize(Policy=...)]),
/// controllers e componentes. Concentrar aqui evita repetir a lógica de role em cada página.
/// </summary>
public static class Politicas
{
    public const string ApenasAdmin = nameof(ApenasAdmin);

    public static void AdicionarPoliticas(AuthorizationOptions options)
    {
        // Toda rota exige usuário autenticado por padrão (FallbackPolicy já definida em Program.cs);
        // esta política restringe ainda mais: somente usuários com a role Admin.
        options.AddPolicy(ApenasAdmin, policy =>
            policy.RequireAuthenticatedUser().RequireRole(ApplicationRole.Admin));
    }
}