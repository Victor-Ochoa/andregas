using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Identity;
using AndreGas.Web.Common.Authorization;
using AndreGas.Web.Features.Auth;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
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
/// <para>
/// <b>Este endpoint reemite uma sessão — logo ele tem de revalidá-la.</b> A primeira versão só
/// exigia <c>RequireAuthorization()</c> e assinava um ticket novo a partir de
/// <c>GetUserAsync</c> + <c>CreateUserPrincipalAsync</c>: um usuário recém-inativado (com o
/// <c>SecurityStamp</c> rotacionado pela central) mas com o cookie ainda dentro da janela de 30 min
/// do <c>SecurityStampValidator</c> chamava este POST e recebia um ticket com o stamp <b>novo</b> —
/// o validador nunca mais o rejeitava, e repetindo o POST a cada &lt;30 min o acesso ficava
/// permanente. Como a aba "Minha Empresa" dispara o POST automaticamente ao salvar, o furo não
/// dependia de ação do atacante. Por isso, hoje, o endpoint:
/// </para>
/// <list type="number">
/// <item>aplica a <b>mesma regra de acesso do login</b> (<see cref="RegraDeAcesso"/>: usuário ativo
/// + empresa ativa) — uma única definição, usada nos dois pontos;</item>
/// <item>valida o <b>SecurityStamp</b> do principal atual contra o do usuário carregado
/// (<c>ValidateSecurityStampAsync</c>), cobrindo troca de senha/role;</item>
/// <item><b>preserva</b> <c>IssuedUtc</c>/<c>ExpiresUtc</c> do ticket atual: reemitir com
/// propriedades vazias reiniciava a validade do cookie a cada POST.</item>
/// </list>
/// <para>
/// Em qualquer recusa a sessão é <b>encerrada</b> (sign-out) e o usuário vai para <c>/login</c> —
/// devolver o cookie antigo intacto deixaria o acesso vivo até a próxima validação de stamp.
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
        // próprio usuário e não altera dado nenhum. As garantias de estado do usuário/empresa e de
        // stamp ficam no próprio endpoint (ver o remark da classe).
        endpoints.MapPost(RotaAtualizarSessao, async (
            HttpContext context,
            IAntiforgery antiforgery,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            AppDbContext db) =>
        {
            if (!await AuthEndpoints.AntiforgeryValidoAsync(context, antiforgery))
            {
                return Results.BadRequest("Requisição inválida (antiforgery).");
            }

            var usuario = await userManager.GetUserAsync(context.User);

            // (1) Mesma regra de acesso do login: usuário ativo + empresa ativa. Usuário nulo
            //     (principal apontando para uma conta que não existe mais) também recusa.
            if (usuario is null
                || await RegraDeAcesso.PodeAutenticarAsync(usuario, db, context.RequestAborted) is not null)
            {
                return await RecusarAsync(context, signInManager);
            }

            // (2) O stamp do principal tem de bater com o do banco. Sem isto, a recarga "lavava" a
            //     revogação por troca de senha/role: o ticket novo nasceria já com o stamp atual.
            if (await signInManager.ValidateSecurityStampAsync(context.User) is null)
            {
                return await RecusarAsync(context, signInManager);
            }

            // Monta o principal pela factory de claims — ela relê a empresa (nome/cor) do banco,
            // então é o que faz o app bar refletir a edição. NÃO renova o SecurityStamp (nem
            // RefreshSignInAsync nem SignInWithClaimsAsync chamam UpdateSecurityStampAsync): a
            // sessão continua a mesma, só o principal é reconstruído.
            var principalNovo = await signInManager.CreateUserPrincipalAsync(usuario);

            // O principal novo vem "limpo" da factory: numa sessão impersonada (o super admin em
            // "Entrar como" também passa por ApenasAdmin e chega nesta tela) ele perderia as
            // claims de impersonation — o badge sumiria e o "Voltar à central" falharia para
            // sempre, prendendo o super admin na empresa. Reaplica pela MESMA lógica fail-closed
            // do SecurityStampValidator (nada duplicado aqui).
            RenovacaoPrincipalImpersonacao.Reaplicar(context.User, principalNovo);

            // (3) Reemite com as propriedades do ticket ATUAL (IssuedUtc/ExpiresUtc/IsPersistent),
            //     em vez de um ticket novo com validade zerada (que estendia o cookie a cada POST).
            var ticketAtual = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            if (!ticketAtual.Succeeded || ticketAtual.Properties is null)
            {
                return await RecusarAsync(context, signInManager);
            }

            await context.SignInAsync(
                IdentityConstants.ApplicationScheme,
                principalNovo,
                ticketAtual.Properties);

            // Carga completa de página: sem isto o circuito antigo (com o principal e o memo de
            // identidade velhos) continuaria servindo o app bar.
            return Results.LocalRedirect("~/configuracoes");
        }).RequireAuthorization();

        return endpoints;
    }

    /// <summary>
    /// Recusa a recarga <b>encerrando</b> a sessão e mandando o usuário para o login. Devolver o
    /// cookie antigo intacto manteria o acesso vivo até a próxima validação de stamp (30 min) — ou
    /// para sempre, se o cliente insistisse no POST.
    /// </summary>
    private static async Task<IResult> RecusarAsync(HttpContext context, SignInManager<ApplicationUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.LocalRedirect("~/login");
    }
}