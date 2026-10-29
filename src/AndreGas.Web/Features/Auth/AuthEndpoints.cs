using AndreGas.Infrastructure.Identity;
using AndreGas.Web.Common.Authorization;
using AndreGas.Web.Features.Central.EntrarComo;
using AndreGas.Web.Features.Central.VoltarCentral;

using Mediator;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace AndreGas.Web.Features.Auth;

/// <summary>
/// Endpoints mínimos de autenticação que não se encaixam no padrão de comando/consulta do
/// Mediator (ex.: logout, que só precisa encerrar a sessão de cookie e redirecionar).
/// </summary>
/// <remarks>
/// Os endpoints de impersonation também vivem aqui porque a operação é, essencialmente, trocar o
/// cookie de autenticação: <c>SignInAsync</c> só funciona com o <c>HttpContext</c> de uma
/// requisição HTTP — não dentro do circuito Blazor. Ambos validam antiforgery, como o
/// <c>/logout</c>. A resposta é sempre um redirect local: a navegação no cliente deve usar
/// <c>forceLoad: true</c> para que o circuito pegue o novo principal e o novo <c>AppDbContext</c>.
/// </remarks>
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

        // "Entrar como": só o super admin da central. O handler reforça a mesma regra no servidor.
        endpoints.MapPost("/central/entrar-como", async (
            HttpContext context,
            IAntiforgery antiforgery,
            ISender sender) =>
        {
            if (!await AntiforgeryValidoAsync(context, antiforgery))
            {
                return Results.BadRequest("Requisição inválida (antiforgery).");
            }

            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            if (!Guid.TryParse(form["empresaId"], out var empresaId) || empresaId == Guid.Empty)
            {
                return Results.BadRequest("Informe a empresa em que deseja entrar.");
            }

            var resultado = await sender.Send(new EntrarComoCommand(empresaId), context.RequestAborted);

            if (resultado.Succeeded)
            {
                return Results.LocalRedirect(resultado.Destino);
            }

            // Recusa de negócio (empresa inativa, sem admin ativo): volta para a central com o
            // motivo na query string para que a tela mostre a mensagem. Um 403 aqui deixava o
            // super admin numa página de erro sem explicação (o cookie transforma Forbid em
            // redirect para /sem-autorizacao).
            var motivo = Uri.EscapeDataString(resultado.Motivo ?? string.Empty);
            return Results.LocalRedirect($"/central?erro={motivo}");
        }).RequireAuthorization(Politicas.ApenasSuperAdmin);

        // "Voltar à central": SEM política de role — o principal impersonado não tem a role
        // SuperAdmin. A segurança vem da claim assinada SuperAdminOriginalId, validada no handler.
        endpoints.MapPost("/central/voltar", async (
            HttpContext context,
            IAntiforgery antiforgery,
            ISender sender) =>
        {
            if (!await AntiforgeryValidoAsync(context, antiforgery))
            {
                return Results.BadRequest("Requisição inválida (antiforgery).");
            }

            var resultado = await sender.Send(new VoltarCentralCommand(), context.RequestAborted);

            return resultado.Succeeded
                ? Results.LocalRedirect(resultado.Destino)
                : Results.Forbid();
        }).RequireAuthorization();

        return endpoints;
    }

    /// <summary>
    /// Valida o token antiforgery devolvendo <c>false</c> (→ 400) em vez de deixar a exceção
    /// <see cref="AntiforgeryValidationException"/> virar 500. O token é obrigatório nos endpoints
    /// de impersonation e no de recarga de sessão (ver <c>ConfiguracaoEmpresaEndpoints</c>).
    /// </summary>
    internal static async Task<bool> AntiforgeryValidoAsync(HttpContext context, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }
}