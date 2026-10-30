using System.Net;
using System.Text.RegularExpressions;

using AndreGas.Domain.Entities;
using AndreGas.Infrastructure.Identity;
using AndreGas.IntegrationTests.TestHelpers;
using AndreGas.Web.Features.Auth;
using AndreGas.Web.Features.ConfiguracaoEmpresa;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.IntegrationTests.Tests;

/// <summary>
/// Prova ponta a ponta que a edição da própria empresa <b>chega ao app bar</b>: nome e cor vivem nas
/// claims do cookie e o logo é memoizado por escopo, então sem reemitir a sessão o admin renomeia a
/// empresa e continua vendo o nome antigo (inclusive depois de um F5).
/// </summary>
/// <remarks>
/// Usa <see cref="WebAppFixture"/> (app web + Postgres reais) porque a prova é o HTML autenticado de
/// <c>/</c> — uma asserção no handler não enxerga claims nem o layout. O fluxo é o real: login SSR →
/// POST de recarga de sessão → GET da home.
/// </remarks>
public class ConfiguracaoEmpresaBrandingTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>, IAsyncLifetime
{
    private const string Senha = "Senha@123";

    private string _email = null!;
    private Guid _empresaId;
    private string _nomeOriginal = null!;

    public async Task InitializeAsync()
    {
        // Empresa + admin reais, com e-mail/slug únicos (o Postgres do AppHost tem volume
        // persistente e outras classes resetam o banco).
        var identificador = $"branding-{Guid.NewGuid():N}";
        _email = $"{identificador}@andregas.com.br";
        _nomeOriginal = $"Empresa Branding {Guid.NewGuid().ToString("N")[..6]}";

        var (empresa, _) = await EmpresaTestHelper.CriarEmpresaEAdminAsync(
            fixture.ConnectionString, _nomeOriginal, identificador, _email, Senha);

        _empresaId = empresa.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Depois de salvar a empresa, o POST de recarga de sessão faz o app bar mostrar o <b>nome novo</b>
    /// — e o controle negativo é o mesmo GET <b>antes</b> do POST, que ainda mostra o antigo.
    /// </summary>
    [Fact]
    public async Task Edicao_da_empresa_atualiza_o_branding_do_app_bar_apos_recarregar_a_sessao()
    {
        var nomeNovo = $"{_nomeOriginal} Renomeada";

        using var client = fixture.CreateClient();
        await AutenticarAsync(client);

        // Controle negativo: antes da recarga, a home autenticada mostra o nome ORIGINAL.
        var antes = await ObterHomeAsync(client);
        Assert.Contains(_nomeOriginal, antes, StringComparison.Ordinal);
        Assert.DoesNotContain(nomeNovo, antes, StringComparison.Ordinal);

        // O admin edita a própria empresa (o mesmo comando que a aba usa).
        await EditarEmpresaAsync(nomeNovo);
        Assert.Equal(nomeNovo, await NomeNoBancoAsync());

        // Ainda sem recarregar a sessão, o cookie antigo mantém o branding velho — é exatamente o
        // bug que o endpoint resolve.
        var semRecarga = await ObterHomeAsync(client);
        Assert.Contains(_nomeOriginal, semRecarga, StringComparison.Ordinal);

        // POST de recarga (o que a aba dispara depois de salvar): reemite o cookie com as claims novas.
        var token = await ObterTokenAntiforgeryAsync(client);
        using var recarga = await client.PostAsync(
            ConfiguracaoEmpresaEndpoints.RotaAtualizarSessao,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.Redirect, recarga.StatusCode);
        Assert.Equal("/configuracoes", recarga.Headers.Location!.OriginalString.TrimEnd('/'));

        // A prova real: a home autenticada passa a exibir o nome novo. (O nome novo CONTÉM o
        // original como prefixo, então o controle negativo é o título completo do painel, que só
        // aparece com o nome atual.)
        var depois = await ObterHomeAsync(client);
        Assert.Contains(nomeNovo, depois, StringComparison.Ordinal);
        Assert.DoesNotContain($"Painel — {_nomeOriginal}<", depois, StringComparison.Ordinal);
        Assert.Contains($"Painel — {nomeNovo}", depois, StringComparison.Ordinal);
    }

    /// <summary>O POST de recarga exige antiforgery: sem o token, 400 (não 500 nem efeito).</summary>
    [Fact]
    public async Task Recarga_de_sessao_sem_antiforgery_e_recusada()
    {
        using var client = fixture.CreateClient();
        await AutenticarAsync(client);

        using var resposta = await client.PostAsync(
            ConfiguracaoEmpresaEndpoints.RotaAtualizarSessao,
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    // ---------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------

    /// <summary>Login SSR real (form estático de <c>/login</c>) com o admin da empresa criada.</summary>
    private async Task AutenticarAsync(HttpClient client)
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
            ["Input.Email"] = _email,
            ["Input.Password"] = Senha,
            ["Input.RememberMe"] = "true",
        }));

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Equal("/", resposta.Headers.Location!.AbsolutePath);
    }

    private static async Task<string> ObterHomeAsync(HttpClient client)
    {
        using var home = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        return await home.Content.ReadAsStringAsync();
    }

    private static async Task<string> ObterTokenAntiforgeryAsync(HttpClient client)
    {
        // O token é lido de uma página SSR estática (login); o cookie antiforgery vale para a sessão.
        using var pagina = await client.GetAsync("/login");
        Assert.Equal(HttpStatusCode.OK, pagina.StatusCode);
        var html = await pagina.Content.ReadAsStringAsync();
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token));
        return token;
    }

    /// <summary>
    /// Executa a edição pelo handler real (mesmo comando da aba), com o tenant da empresa e um
    /// principal Admin — o caminho de escrita é o de produção; só a UI é substituída por esta chamada.
    /// </summary>
    private async Task EditarEmpresaAsync(string nomeNovo)
    {
        var (userManager, _, provider) = IdentityTestHelpers.CriarManagers(fixture.ConnectionString);
        try
        {
            var tenant = new FakeTenantContext(_empresaId);
            var db = new AndreGas.Infrastructure.AppDbContext(
                new DbContextOptionsBuilder<AndreGas.Infrastructure.AppDbContext>()
                    .UseNpgsql(fixture.ConnectionString)
                    .AddInterceptors(new AndreGas.Infrastructure.Tenancy.SetEmpresaIdInterceptor(tenant))
                    .Options,
                tenant);

            await using (db)
            {
                var usuario = await userManager.FindByEmailAsync(_email);
                Assert.NotNull(usuario);

                var authProvider = new FakeAuthenticationStateProvider(_email, ApplicationRole.Admin);
                var usuarioAutenticado = new AndreGas.Web.Common.Authorization.UsuarioAutenticado(authProvider, userManager);

                var handler = new EditarConfigEmpresaCommandHandler(db, tenant, usuarioAutenticado, authProvider);

                var editado = await handler.Handle(
                    new EditarConfigEmpresaCommand(nomeNovo), CancellationToken.None);

                Assert.True(editado);
            }
        }
        finally
        {
            provider.Dispose();
        }
    }

    private async Task<string> NomeNoBancoAsync()
    {
        await using var db = fixture.CreateDbContext();
        return await db.Set<Empresa>().Where(e => e.Id == _empresaId).Select(e => e.Nome).SingleAsync();
    }

    /// <summary>Principal de teste: nome + role, autenticado (usado na auditoria da edição).</summary>
    private sealed class FakeAuthenticationStateProvider : Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider
    {
        private readonly Microsoft.AspNetCore.Components.Authorization.AuthenticationState _state;

        public FakeAuthenticationStateProvider(string nome, string papel)
        {
            var identity = new System.Security.Claims.ClaimsIdentity(
                [
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, nome),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, papel),
                ],
                authenticationType: "Teste");

            _state = new Microsoft.AspNetCore.Components.Authorization.AuthenticationState(
                new System.Security.Claims.ClaimsPrincipal(identity));
        }

        public override Task<Microsoft.AspNetCore.Components.Authorization.AuthenticationState>
            GetAuthenticationStateAsync() => Task.FromResult(_state);
    }
}