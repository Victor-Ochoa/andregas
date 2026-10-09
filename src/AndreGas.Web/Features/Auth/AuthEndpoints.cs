using AndreGas.Infrastructure.Identity;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;

namespace AndreGas.Web.Features.Auth;

/// <summary>
/// Endpoints mínimos de autenticação que não se encaixam no padrão de comando/consulta do
/// Mediator (ex.: logout, que só precisa encerrar a sessão de cookie e redirecionar).
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/logout", async (HttpContext context, IAntiforgery antiforgery, SignInManager<ApplicationUser> signInManager) =>
        {
            await antiforgery.ValidateRequestAsync(context);
            await signInManager.SignOutAsync();
            return Results.LocalRedirect("~/login");
        });

        return endpoints;
    }
}