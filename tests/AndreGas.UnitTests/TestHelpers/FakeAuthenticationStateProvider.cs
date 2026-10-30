using System.Security.Claims;

using Microsoft.AspNetCore.Components.Authorization;

namespace AndreGas.UnitTests.TestHelpers;

/// <summary>
/// <see cref="AuthenticationStateProvider"/> falso para testes: retorna um principal nomeado
/// (usuário autenticado) ou anônimo, permitindo exercitar os fluxos de "quem fez" da auditoria.
/// </summary>
public sealed class FakeAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly AuthenticationState _state;

    public FakeAuthenticationStateProvider(string nome)
    {
        var claims = new[] { new Claim(ClaimTypes.Name, nome) };
        var identity = new ClaimsIdentity(claims, "Fake");
        _state = new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public FakeAuthenticationStateProvider(bool autenticado)
    {
        var identity = autenticado
            ? new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "teste@andregas.com.br") }, "Fake")
            : new ClaimsIdentity();
        _state = new AuthenticationState(new ClaimsPrincipal(identity));
    }

    /// <summary>Aceita claims arbitrários (ex.: <c>ClaimTypes.Role</c>) para exercitar o caminho
    /// rápido do <see cref="AndreGas.Web.Common.Authorization.UsuarioAutenticado"/>. Principal
    /// autenticado (authenticationType "Fake").</summary>
    public FakeAuthenticationStateProvider(params Claim[] claims)
    {
        var identity = claims.Length == 0
            ? new ClaimsIdentity()
            : new ClaimsIdentity(claims, "Fake");
        _state = new AuthenticationState(new ClaimsPrincipal(identity));
    }

    /// <summary>Cria um principal com claims escolhendo se está autenticado. Com
    /// <paramref name="autenticado"/>=false o identity carrega claims mas permanece anônimo —
    /// exercita a guarda do <see cref="AndreGas.Web.Common.Authorization.UsuarioAutenticado"/>
    /// (não confiar em claims de principal anônimo).</summary>
    public FakeAuthenticationStateProvider(IEnumerable<Claim> claims, bool autenticado)
    {
        var identity = autenticado
            ? new ClaimsIdentity(claims, "Fake")
            : new ClaimsIdentity(claims);
        _state = new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(_state);
}