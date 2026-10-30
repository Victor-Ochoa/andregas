using System.Net;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using RevendaApp.Domain.Entities;
using RevendaApp.Infrastructure.Identity;
using RevendaApp.IntegrationTests.TestHelpers;
using RevendaApp.Web.Features.ConfiguracaoEmpresa;

namespace RevendaApp.IntegrationTests.Tests;

/// <summary>
/// Prova que o <c>POST /configuracoes/atualizar-sessao</c> (recarga de sessão depois de editar a
/// empresa) <b>não ressuscita uma sessão revogada</b> e <b>não estende a validade do cookie</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Regressão que este teste trava (A1):</b> o endpoint só exigia <c>RequireAuthorization()</c> e
/// reemitia o cookie com <c>CreateUserPrincipalAsync</c> + <c>SignInAsync</c> <b>sem</b> revalidar
/// <c>ApplicationUser.Ativo</c>, <c>Empresa.Ativo</c> nem o <c>SecurityStamp</c>. Um usuário
/// recém-inativado (que teve o stamp rotacionado pela central) com o cookie ainda dentro da janela
/// de 30 min do <c>SecurityStampValidator</c> chamava esse POST e recebia um ticket novo já com o
/// stamp novo — o validador nunca mais o rejeitava, e repetindo o POST a cada &lt;30 min o acesso
/// ficava <b>permanente</b>. Agravante: <c>ConfigEmpresaTab.razor</c> dispara esse POST
/// automaticamente ao salvar.
/// </para>
/// <para>
/// Estes testes são HTTP (app + Postgres reais via <see cref="WebAppFixture"/>): a garantia é o
/// comportamento do <b>cookie</b> e do pipeline de autenticação, que um teste de unidade do
/// endpoint não enxerga.
/// </para>
/// </remarks>
public class ConfiguracaoEmpresaSessaoRevogadaTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    private const string Senha = "Senha@123";

    /// <summary>Nome do cookie de autenticação do Identity (usado para ler a validade emitida).</summary>
    private const string CookieDeAutenticacao = ".AspNetCore.Identity.Application";

    /// <summary>
    /// Usuário inativado pela central (Ativo=false + stamp rotacionado, a mesma sequência do
    /// <c>InativarUsuarioEmpresaCommandHandler</c>): a recarga de sessão é <b>recusada</b> e a sessão
    /// cai — <c>/</c> volta a exigir login.
    /// </summary>
    [Fact]
    public async Task Recarga_de_sessao_recusa_usuario_inativado_e_derruba_a_sessao()
    {
        var (email, _) = await CriarEmpresaEAdminAsync();

        using var client = fixture.CreateClient();
        await AutenticarAsync(client, email);

        // Controle positivo: a sessão está válida antes da inativação.
        using (var antes = await client.GetAsync("/"))
        {
            Assert.Equal(HttpStatusCode.OK, antes.StatusCode);
        }

        await InativarUsuarioAsync(email);

        var token = await ObterTokenAntiforgeryAsync(client);
        using var recarga = await PostRecargaAsync(client, token);

        // Recusado: não reemite a sessão (antes: 302 para /configuracoes com o cookie renovado).
        AssertRedirecionadoParaLogin(recarga);

        // E a sessão revogada não sobrevive: a home volta a exigir login.
        using var depois = await client.GetAsync("/");
        AssertRedirecionadoParaLogin(depois);
    }

    /// <summary>
    /// Usuário inativado <b>sem</b> rotação de stamp (o caso em que a rotação falha e o handler da
    /// central reporta "sessão não revogada"): a recarga continua recusada porque a regra de acesso
    /// olha <c>ApplicationUser.Ativo</c> — não só o stamp.
    /// </summary>
    [Fact]
    public async Task Recarga_de_sessao_recusa_usuario_inativado_sem_rotacao_de_stamp()
    {
        var (email, _) = await CriarEmpresaEAdminAsync();

        using var client = fixture.CreateClient();
        await AutenticarAsync(client, email);

        // Só `Ativo = false`: o stamp do cookie continua válido.
        await MarcarUsuarioInativoSemRotacionarStampAsync(email);

        var token = await ObterTokenAntiforgeryAsync(client);
        using var recarga = await PostRecargaAsync(client, token);

        AssertRedirecionadoParaLogin(recarga);

        using var depois = await client.GetAsync("/");
        AssertRedirecionadoParaLogin(depois);
    }

    /// <summary>
    /// Usuário <b>ativo</b>, mas com o <c>SecurityStamp</c> rotacionado (troca de senha/role):
    /// a recarga também é recusada — é o mesmo furo do A1 pela porta do stamp, que
    /// <c>GetUserAsync</c> sozinho não fecha.
    /// </summary>
    [Fact]
    public async Task Recarga_de_sessao_recusa_quando_o_security_stamp_mudou()
    {
        var (email, _) = await CriarEmpresaEAdminAsync();

        using var client = fixture.CreateClient();
        await AutenticarAsync(client, email);

        await RotacionarSecurityStampAsync(email);

        var token = await ObterTokenAntiforgeryAsync(client);
        using var recarga = await PostRecargaAsync(client, token);

        AssertRedirecionadoParaLogin(recarga);

        using var depois = await client.GetAsync("/");
        AssertRedirecionadoParaLogin(depois);
    }

    /// <summary>
    /// Empresa desativada: mesmo com usuário ativo e stamp válido, a recarga é recusada (a regra de
    /// acesso é a mesma do login — sem ela, a edição da própria empresa manteria viva uma sessão de
    /// um tenant desativado).
    /// </summary>
    [Fact]
    public async Task Recarga_de_sessao_recusa_empresa_desativada()
    {
        var (email, empresaId) = await CriarEmpresaEAdminAsync();

        using var client = fixture.CreateClient();
        await AutenticarAsync(client, email);

        await DesativarEmpresaAsync(empresaId);

        var token = await ObterTokenAntiforgeryAsync(client);
        using var recarga = await PostRecargaAsync(client, token);

        AssertRedirecionadoParaLogin(recarga);

        using var depois = await client.GetAsync("/");
        AssertRedirecionadoParaLogin(depois);
    }

    /// <summary>
    /// A recarga <b>preserva</b> <c>IssuedUtc</c>/<c>ExpiresUtc</c> do ticket atual: sem isso cada
    /// POST estendia a validade do cookie (validade nova = agora + 14 dias).
    /// </summary>
    /// <remarks>
    /// A prova é a data <c>Expires</c> do cookie reemitido: com a preservação ela é <b>idêntica</b>
    /// à emitida no login; sem ela seria "agora + ExpireTimeSpan". O intervalo de 2s entre login e
    /// recarga garante que a diferença (de segundos) apareça na string RFC1123 do cookie.
    /// </remarks>
    [Fact]
    public async Task Recarga_de_sessao_preserva_a_validade_do_cookie()
    {
        var (email, _) = await CriarEmpresaEAdminAsync();

        using var client = fixture.CreateClient();
        var validadeDoLogin = await AutenticarEObterValidadeDoCookieAsync(client, email);
        Assert.NotNull(validadeDoLogin);

        await Task.Delay(TimeSpan.FromSeconds(2));

        var token = await ObterTokenAntiforgeryAsync(client);
        using var recarga = await PostRecargaAsync(client, token);
        Assert.Equal(HttpStatusCode.Redirect, recarga.StatusCode);
        Assert.Equal("/configuracoes", recarga.Headers.Location!.OriginalString.TrimEnd('/'));

        var validadeDaRecarga = LerValidadeDoCookie(recarga);
        Assert.NotNull(validadeDaRecarga);
        Assert.Equal(validadeDoLogin, validadeDaRecarga);
    }

    // ---------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------

    private static void AssertRedirecionadoParaLogin(HttpResponseMessage resposta)
    {
        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Contains("/login", resposta.Headers.Location!.OriginalString, StringComparison.Ordinal);
    }

    private static Task<HttpResponseMessage> PostRecargaAsync(HttpClient client, string token) =>
        client.PostAsync(
            ConfiguracaoEmpresaEndpoints.RotaAtualizarSessao,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
            }));

    /// <summary>Validade (<c>Expires</c>) do cookie de autenticação emitido na resposta.</summary>
    private static string? LerValidadeDoCookie(HttpResponseMessage resposta)
    {
        var cabecalhos = resposta.Headers.TryGetValues("Set-Cookie", out var valores)
            ? valores
            : [];

        var cookie = cabecalhos.FirstOrDefault(c =>
            c.StartsWith(CookieDeAutenticacao + "=", StringComparison.Ordinal));

        if (cookie is null)
        {
            return null;
        }

        return Regex.Match(cookie, "expires=([^;]+)", RegexOptions.IgnoreCase).Groups[1].Value;
    }

    private static async Task<string?> AutenticarEObterValidadeDoCookieAsync(HttpClient client, string email)
    {
        using var pagina = await client.GetAsync("/login");
        var html = await pagina.Content.ReadAsStringAsync();

        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"").Groups[1].Value;
        var handler = Regex.Match(html, "name=\"_handler\" value=\"([^\"]*)\"").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token));
        Assert.False(string.IsNullOrEmpty(handler));

        using var resposta = await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = handler,
            ["__RequestVerificationToken"] = token,
            ["Input.Email"] = email,
            ["Input.Password"] = Senha,
            // Persistente: é o que faz o cookie carregar `Expires` (sem isso é cookie de sessão).
            ["Input.RememberMe"] = "true",
        }));

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        return LerValidadeDoCookie(resposta);
    }

    private static async Task AutenticarAsync(HttpClient client, string email)
    {
        using var pagina = await client.GetAsync("/login");
        var html = await pagina.Content.ReadAsStringAsync();

        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"").Groups[1].Value;
        var handler = Regex.Match(html, "name=\"_handler\" value=\"([^\"]*)\"").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token));
        Assert.False(string.IsNullOrEmpty(handler));

        using var resposta = await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = handler,
            ["__RequestVerificationToken"] = token,
            ["Input.Email"] = email,
            ["Input.Password"] = Senha,
            ["Input.RememberMe"] = "true",
        }));

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Equal("/", resposta.Headers.Location!.AbsolutePath);
    }

    private static async Task<string> ObterTokenAntiforgeryAsync(HttpClient client)
    {
        // /login é SSR estática e [AllowAnonymous]: o token vale para a sessão (cookie antiforgery).
        using var resposta = await client.GetAsync("/login");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var html = await resposta.Content.ReadAsStringAsync();
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token));
        return token;
    }

    /// <summary>Empresa + admin reais (e-mail/slug únicos: o Postgres do AppHost é persistente).</summary>
    private async Task<(string Email, Guid EmpresaId)> CriarEmpresaEAdminAsync()
    {
        var identificador = $"sessao-{Guid.NewGuid():N}";
        var email = $"{identificador}@revenda.app.com.br";

        var (empresa, _) = await EmpresaTestHelper.CriarEmpresaEAdminAsync(
            fixture.ConnectionString, $"Empresa Sessão {identificador[..12]}", identificador, email, Senha);

        return (email, empresa.Id);
    }

    /// <summary>
    /// Inativa o usuário exatamente como a central faz: <c>Ativo = false</c> + rotação do
    /// <c>SecurityStamp</c> (o passo que revoga a sessão viva).
    /// </summary>
    private async Task InativarUsuarioAsync(string email)
    {
        var (userManager, _, provider) = IdentityTestHelpers.CriarManagers(fixture.ConnectionString);
        try
        {
            var usuario = await userManager.FindByEmailAsync(email);
            Assert.NotNull(usuario);

            usuario.Ativo = false;
            var atualizacao = await userManager.UpdateAsync(usuario);
            Assert.True(atualizacao.Succeeded, string.Join("; ", atualizacao.Errors.Select(e => e.Description)));

            var stamp = await userManager.UpdateSecurityStampAsync(usuario);
            Assert.True(stamp.Succeeded, string.Join("; ", stamp.Errors.Select(e => e.Description)));
        }
        finally
        {
            provider.Dispose();
        }
    }

    /// <summary>
    /// Inativa o usuário <b>sem</b> tocar no <c>SecurityStamp</c> — o cenário em que a rotação do
    /// stamp falhou (o handler da central responde "sessão não revogada") e o único sinal é
    /// <c>Ativo</c>.
    /// </summary>
    private async Task MarcarUsuarioInativoSemRotacionarStampAsync(string email)
    {
        var (userManager, _, provider) = IdentityTestHelpers.CriarManagers(fixture.ConnectionString);
        try
        {
            var usuario = await userManager.FindByEmailAsync(email);
            Assert.NotNull(usuario);

            usuario.Ativo = false;
            var atualizacao = await userManager.UpdateAsync(usuario);
            Assert.True(atualizacao.Succeeded, string.Join("; ", atualizacao.Errors.Select(e => e.Description)));
        }
        finally
        {
            provider.Dispose();
        }
    }

    /// <summary>Usuário continua ativo; só o stamp muda (equivale a troca de senha/role).</summary>
    private async Task RotacionarSecurityStampAsync(string email)
    {
        var (userManager, _, provider) = IdentityTestHelpers.CriarManagers(fixture.ConnectionString);
        try
        {
            var usuario = await userManager.FindByEmailAsync(email);
            Assert.NotNull(usuario);

            var stamp = await userManager.UpdateSecurityStampAsync(usuario);
            Assert.True(stamp.Succeeded, string.Join("; ", stamp.Errors.Select(e => e.Description)));
        }
        finally
        {
            provider.Dispose();
        }
    }

    private async Task DesativarEmpresaAsync(Guid empresaId)
    {
        await using var db = fixture.CreateDbContext();
        var empresa = await db.Set<Empresa>().SingleAsync(e => e.Id == empresaId);
        empresa.Desativar();
        await db.SaveChangesAsync();
    }
}