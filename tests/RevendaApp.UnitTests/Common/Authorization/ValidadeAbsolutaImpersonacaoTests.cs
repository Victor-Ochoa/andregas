using System.Globalization;
using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using RevendaApp.Web.Common.Authorization;
using RevendaApp.Web.Common.Tenancy;

namespace RevendaApp.UnitTests.Common.Authorization;

/// <summary>
/// Testes do <b>teto absoluto</b> da sessão de impersonation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Problema que resolve:</b> o "Entrar como" prometia um teto de 8h, mas o cookie do Identity usa
/// os defaults (<c>SlidingExpiration = true</c>, <c>ExpireTimeSpan = 14 dias</c>) e o
/// <c>SecurityStampValidator</c> também renova o ticket (<c>ShouldRenew</c>). Numa sessão
/// <b>em uso</b> o ticket nunca vence — o teto só valia para sessão ociosa. Além disso,
/// <c>AllowRefresh = false</c>/<c>SlidingExpiration = false</c> sozinhos <b>não</b> bastam, porque a
/// renovação do validador de stamp passa por fora do gate de <c>AllowRefresh</c>.
/// </para>
/// <para>
/// A garantia é independente do cookie: a claim <see cref="TenantContext.ClaimImpersonacaoIniciadaEm"/>
/// (UTC, escrita pelo "Entrar como") é comparada com o <see cref="TimeProvider"/> a cada validação
/// de principal. Passado o teto, a sessão é <b>encerrada</b>.
/// </para>
/// </remarks>
public class ValidadeAbsolutaImpersonacaoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private static ClaimsPrincipal CriarPrincipalImpersonado(DateTimeOffset? iniciadaEm, bool impersonando = true)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(TenantContext.ClaimEmpresaId, Guid.NewGuid().ToString()),
        };

        if (impersonando)
        {
            claims.Add(new Claim(TenantContext.ClaimImpersonando, "true"));
            claims.Add(new Claim(TenantContext.ClaimSuperAdminOriginalId, Guid.NewGuid().ToString()));
        }

        if (iniciadaEm is { } inicio)
        {
            claims.Add(new Claim(
                TenantContext.ClaimImpersonacaoIniciadaEm,
                inicio.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Identity.Application"));
    }

    /// <summary>Contexto de validação do cookie com um <c>TimeProvider</c> fixo em <see cref="Agora"/>.</summary>
    private static CookieValidatePrincipalContext CriarContexto(
        ClaimsPrincipal principal, StubAuthenticationService? auth = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(Agora));
        services.AddSingleton<IAuthenticationService>(auth ?? new StubAuthenticationService());
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        var options = new CookieAuthenticationOptions();
        var ticket = new AuthenticationTicket(principal, "Identity.Application");

        return new CookieValidatePrincipalContext(
            httpContext,
            new AuthenticationScheme("Identity.Application", "Identity.Application", typeof(CookieAuthenticationHandler)),
            options,
            ticket);
    }

    [Fact]
    public async Task Sessao_impersonada_dentro_do_teto_continua_valida()
    {
        // 7h59 desde o início: dentro das 8h.
        var principal = CriarPrincipalImpersonado(Agora.AddHours(-8).AddMinutes(1));
        var auth = new StubAuthenticationService();
        var context = CriarContexto(principal, auth);

        await ValidadeAbsolutaImpersonacao.EncerrarSeExpiradaAsync(context);

        Assert.NotNull(context.Principal);
        Assert.Equal(0, auth.SignOuts);
    }

    [Fact]
    public async Task Sessao_impersonada_alem_do_teto_e_encerrada()
    {
        // 8h01 desde o início: estourou o teto absoluto.
        var principal = CriarPrincipalImpersonado(Agora.AddHours(-8).AddMinutes(-1));
        var auth = new StubAuthenticationService();
        var context = CriarContexto(principal, auth);

        await ValidadeAbsolutaImpersonacao.EncerrarSeExpiradaAsync(context);

        Assert.Null(context.Principal);
        Assert.Equal(1, auth.SignOuts);
    }

    [Fact]
    public async Task Sessao_nao_impersonada_nao_e_afetada_pelo_teto_de_impersonacao()
    {
        // Operador comum com um principal "antigo" (sem impersonation): o teto não se aplica.
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
            "Identity.Application"));
        var auth = new StubAuthenticationService();
        var context = CriarContexto(principal, auth);

        await ValidadeAbsolutaImpersonacao.EncerrarSeExpiradaAsync(context);

        Assert.NotNull(context.Principal);
        Assert.Equal(0, auth.SignOuts);
    }

    /// <summary>
    /// Fail-closed: um principal que se diz impersonado <b>sem</b> a marca de início não tem teto
    /// calculável — é encerrado em vez de durar para sempre.
    /// </summary>
    [Fact]
    public async Task Sessao_impersonada_sem_marca_de_inicio_e_encerrada()
    {
        var principal = CriarPrincipalImpersonado(iniciadaEm: null);
        var auth = new StubAuthenticationService();
        var context = CriarContexto(principal, auth);

        await ValidadeAbsolutaImpersonacao.EncerrarSeExpiradaAsync(context);

        Assert.Null(context.Principal);
        Assert.Equal(1, auth.SignOuts);
    }

    /// <summary>
    /// Prova a LIGAÇÃO usada por <c>Program.cs</c>: dentro do teto, o handler anterior (o
    /// <c>SecurityStampValidator.ValidatePrincipalAsync</c>) continua sendo chamado — substituí-lo
    /// desligaria a invalidação por troca de senha/inativação.
    /// </summary>
    [Fact]
    public async Task Configurar_encadeia_o_handler_anterior_quando_a_sessao_esta_dentro_do_teto()
    {
        var options = new CookieAuthenticationOptions();
        var anteriorExecutado = false;
        options.Events.OnValidatePrincipal = _ =>
        {
            anteriorExecutado = true;
            return Task.CompletedTask;
        };

        ValidadeAbsolutaImpersonacao.Configurar(options);

        Assert.NotNull(options.Events.OnValidatePrincipal);

        var principal = CriarPrincipalImpersonado(Agora.AddHours(-1));
        var auth = new StubAuthenticationService();
        var context = CriarContexto(principal, auth);

        await options.Events.OnValidatePrincipal(context);

        Assert.True(anteriorExecutado);
        Assert.NotNull(context.Principal);
        Assert.Equal(0, auth.SignOuts);
    }

    /// <summary>
    /// Prova a LIGAÇÃO no caso que importa: além do teto, a sessão é encerrada pelo callback
    /// registrado (não só pelo helper) — e o handler anterior nem chega a rodar.
    /// </summary>
    [Fact]
    public async Task Configurar_encerra_a_sessao_alem_do_teto_pelo_callback_registrado()
    {
        var options = new CookieAuthenticationOptions();
        var anteriorExecutado = false;
        options.Events.OnValidatePrincipal = _ =>
        {
            anteriorExecutado = true;
            return Task.CompletedTask;
        };

        ValidadeAbsolutaImpersonacao.Configurar(options);

        var principal = CriarPrincipalImpersonado(Agora.AddHours(-9));
        var auth = new StubAuthenticationService();
        var context = CriarContexto(principal, auth);

        await options.Events.OnValidatePrincipal!(context);

        Assert.Null(context.Principal);
        Assert.Equal(1, auth.SignOuts);
        Assert.False(anteriorExecutado);
    }

    private sealed class FixedTimeProvider(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }

    /// <summary>Stub de <see cref="IAuthenticationService"/>: conta os sign-outs (encerramentos).</summary>
    private sealed class StubAuthenticationService : IAuthenticationService
    {
        public int SignOuts { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task SignInAsync(
            HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            SignOuts++;
            return Task.CompletedTask;
        }
    }
}