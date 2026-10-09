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

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(_state);
}