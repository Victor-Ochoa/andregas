using AndreGas.Infrastructure.Identity;
using AndreGas.Web.Common.Authorization;
using AndreGas.Web.Features.Auth;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;

namespace AndreGas.Web.Features.ConfiguracaoEmpresa;

/// <summary>
/// Endpoint que <b>reemite o cookie</b> do usuário autenticado depois de ele editar os dados da
/// própria empresa.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por que existe:</b> nome e cor da empresa vivem nas <b>claims</b> do cookie
/// (<c>EmpresaNome</c>/<c>EmpresaCor</c>) e o logo é memoizado por escopo em
/// <see cref="AndreGas.Web.Common.IdentidadeEmpresaService"/>. Editar a empresa sem reemitir o
/// cookie deixaria o app bar (e o tema) com o nome/cor antigos até o próximo login — inclusive
/// depois de recarregar a página, porque as claims continuam no cookie.
/// </para>
/// <para>
/// <b>Forma igual à do <c>/logout</c> e do <c>/central/voltar</c>:</b> um <c>POST</c> com
/// antiforgery obrigatório, submetido por um <c>&lt;form method="post"&gt;</c> nativo (o
/// <c>SignInManager</c> precisa do <c>HttpContext</c>, que não existe dentro do circuito Blazor).
/// A resposta é um redirect local, ou seja, <b>carga completa de página</b>: as claims novas e um
/// <see cref="AndreGas.Web.Common.IdentidadeEmpresaService"/> novo (memo do logo limpo) passam a
/// valer.
/// </para>
/// </remarks>
public static class ConfiguracaoEmpresaEndpoints
{
    /// <summary>Rota do POST de recarga de sessão após editar a empresa.</summary>
    public const string RotaAtualizarSessao = "/configuracoes/atualizar-sessao";

    public static IEndpointRouteBuilder MapConfiguracaoEmpresaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Só quem está autenticado: não há política de role aqui de propósito — a edição em si é
        // autorizada no handler (RequerAdminAsync); este endpoint apenas reemite o cookie do
        // próprio usuário e não altera dado nenhum.
        endpoints.MapPost(RotaAtualizarSessao, async (
            HttpContext context,
            IAntiforgery antiforgery,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager) =>
        {
            if (!await AuthEndpoints.AntiforgeryValidoAsync(context, antiforgery))
            {
                return Results.BadRequest("Requisição inválida (antiforgery).");
            }

            var usuario = await userManager.GetUserAsync(context.User);
            if (usuario is not null)
            {
                // RefreshSignInAsync reemite o principal pela factory de claims, que relê a empresa
                // (nome/cor) do banco. Renova também o SecurityStamp: é o caminho suportado do
                // Identity para "o usuário mudou e a sessão precisa refletir isso".
                await signInManager.RefreshSignInAsync(usuario);
            }

            // Carga completa de página: sem isto o circuito antigo (com o principal e o memo de
            // identidade velhos) continuaria servindo o app bar.
            return Results.LocalRedirect("~/configuracoes");
        }).RequireAuthorization();

        return endpoints;
    }
}