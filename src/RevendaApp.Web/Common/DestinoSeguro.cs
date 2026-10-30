namespace RevendaApp.Web.Common;

/// <summary>
/// Decide se um <c>returnUrl</c> vindo da query string pode ser usado como destino de navegação
/// após o login.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bug que isto fecha:</b> <c>/login?returnUrl=https://site-malicioso</c> fazia o login bem
/// sucedido navegar o usuário para fora da aplicação — um <i>open redirect</i>. O atacante monta o
/// link com o domínio real na frente, a vítima confia, digita a senha de verdade e é entregue a uma
/// página falsa. Não há vazamento de credencial no servidor, mas a credibilidade do domínio é
/// usada para phishing.
/// </para>
/// <para>
/// A regra é a mesma do <c>Url.IsLocalUrl</c> do ASP.NET Core: só caminho absoluto local. Rejeita
/// esquema (<c>https:</c>, <c>javascript:</c>), URL relativa de protocolo (<c>//evil.com</c>) e a
/// variante com barra invertida (<c>/\evil.com</c>), que o navegador normaliza para
/// <c>//evil.com</c>.
/// </para>
/// </remarks>
public static class DestinoSeguro
{
    /// <summary>
    /// Devolve <paramref name="returnUrl"/> quando ele é um caminho local seguro; caso contrário,
    /// <paramref name="destinoPadrao"/>.
    /// </summary>
    public static string OuPadrao(string? returnUrl, string destinoPadrao) =>
        EhLocal(returnUrl) ? returnUrl! : destinoPadrao;

    /// <summary>
    /// Verdadeiro somente para caminho absoluto local: começa com <c>/</c>, não é
    /// <c>//host</c> nem <c>/\host</c> e não contém esquema.
    /// </summary>
    public static bool EhLocal(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || returnUrl[0] != '/')
        {
            return false;
        }

        // "//host" e "/\host" são interpretados pelo navegador como autoridade externa.
        if (returnUrl.Length > 1 && (returnUrl[1] == '/' || returnUrl[1] == '\\'))
        {
            return false;
        }

        // Qualquer coisa com esquema ("javascript:alert(1)", "https://x") não é caminho local.
        // Um caminho local legítimo nunca tem ':' antes da primeira '/'.
        var primeiraBarra = returnUrl.IndexOf('/', 1);
        var fim = primeiraBarra < 0 ? returnUrl.Length : primeiraBarra;

        return returnUrl.AsSpan(0, fim).IndexOf(':') < 0;
    }
}