using System.Globalization;
using System.Security.Claims;

using AndreGas.Web.Common.Tenancy;
using AndreGas.Web.Features.Central.EntrarComo;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AndreGas.Web.Common.Authorization;

/// <summary>
/// Aplica o <b>teto absoluto</b> da sessão de impersonation ("Entrar como"): passado
/// <see cref="EntrarComoCommandHandler.ValidadeImpersonacao"/> desde o início, o cookie é
/// <b>rejeitado</b> e o usuário volta ao login.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por que um claim e não só o cookie:</b> o cookie do Identity é <i>sliding</i>
/// (<c>SlidingExpiration = true</c>, <c>ExpireTimeSpan</c> de 14 dias) e o
/// <c>SecurityStampValidator</c> chama <c>RequestRefresh</c> por conta própria. Numa sessão
/// <b>em uso</b> o ticket é renovado continuamente e o <c>ExpiresUtc</c> definido no
/// <c>SignInAsync</c> do "Entrar como" nunca é alcançado: o teto valia só para sessão ociosa.
/// <c>AllowRefresh = false</c>/<c>SlidingExpiration = false</c> isolados também não bastam, porque
/// a renovação do validador de stamp passa por fora do gate de <c>AllowRefresh</c>.
/// </para>
/// <para>
/// A marca de início (<see cref="TenantContext.ClaimImpersonacaoIniciadaEm"/>) é escrita uma única
/// vez, no "Entrar como", e <b>nunca</b> é renovada pelas reemissões do principal: o callback do
/// <c>SecurityStampValidator</c> e o <c>POST /configuracoes/atualizar-sessao</c> copiam as claims de
/// impersonation do principal anterior, com o valor original. Comparada com o <see cref="TimeProvider"/>
/// a cada validação, ela é a única medida de duração que o cliente não consegue esticar.
/// </para>
/// <para>
/// <b>Fail-closed:</b> um principal que se diz impersonado mas não traz a marca (ou a traz em
/// formato inválido) é encerrado — sem marca não há como limitar a sessão, e aceitar seria voltar ao
/// comportamento sem teto.
/// </para>
/// <para>
/// <b>Encadeamento obrigatório:</b> este callback <b>substitui</b> o
/// <c>SecurityStampValidator.ValidatePrincipalAsync</c> que o <c>AddIdentity</c> registra. Por isso
/// <see cref="Configurar"/> guarda e chama o handler anterior — removê-lo desligaria a invalidação de
/// sessão por troca de senha/inativação de usuário.
/// </para>
/// </remarks>
public static class ValidadeAbsolutaImpersonacao
{
    /// <summary>
    /// Encerra a sessão quando ela é uma impersonation além do teto absoluto. Devolve <c>true</c>
    /// quando encerrou (e então nenhum outro handler deve revalidar o principal).
    /// </summary>
    public static async Task<bool> EncerrarSeExpiradaAsync(CookieValidatePrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!Expirada(context.Principal, ObterAgora(context.HttpContext)))
        {
            return false;
        }

        // RejectPrincipal faz o handler do cookie tratar o request como não autenticado (o
        // middleware de autorização manda para /login). O sign-out apaga o cookie de fato, para o
        // cliente não reenviar um ticket já expirado.
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(context.Scheme.Name, properties: null);
        return true;
    }

    /// <summary>
    /// Verdadeiro quando o principal é de uma sessão impersonada que já passou do teto absoluto.
    /// Um principal não impersonado nunca expira por esta regra; um principal impersonado
    /// <b>sem</b> marca de início expira (fail-closed).
    /// </summary>
    public static bool Expirada(ClaimsPrincipal? principal, DateTimeOffset agora)
    {
        if (principal is null || !EstaImpersonando(principal))
        {
            return false;
        }

        var valor = principal.FindFirst(TenantContext.ClaimImpersonacaoIniciadaEm)?.Value;
        if (!DateTimeOffset.TryParse(
                valor,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var inicio))
        {
            // Sem marca calculável não há teto: fail-closed.
            return true;
        }

        return agora - inicio > EntrarComoCommandHandler.ValidadeImpersonacao;
    }

    /// <summary>
    /// Liga a validação no <see cref="CookieAuthenticationOptions"/> <b>preservando</b> o
    /// <c>OnValidatePrincipal</c> já registrado (o do <c>SecurityStampValidator</c>). Chamado por
    /// <c>Program.cs</c>; isolado aqui para que um teste possa verificar a <b>ligação</b>, que é
    /// justamente o que se perde se alguém remover o registro.
    /// </summary>
    public static void Configurar(CookieAuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var anterior = options.Events.OnValidatePrincipal;

        options.Events.OnValidatePrincipal = async context =>
        {
            if (await EncerrarSeExpiradaAsync(context))
            {
                return;
            }

            if (anterior is not null)
            {
                await anterior(context);
            }
        };
    }

    private static DateTimeOffset ObterAgora(HttpContext httpContext) =>
        httpContext.RequestServices?.GetService<TimeProvider>()?.GetUtcNow() ?? DateTimeOffset.UtcNow;

    private static bool EstaImpersonando(ClaimsPrincipal principal) =>
        bool.TryParse(principal.FindFirst(TenantContext.ClaimImpersonando)?.Value, out var valor) && valor;
}