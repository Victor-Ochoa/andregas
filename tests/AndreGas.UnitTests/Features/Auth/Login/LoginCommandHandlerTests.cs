using AndreGas.Web.Features.Auth.Login;

namespace AndreGas.UnitTests.Features.Auth.Login;

/// <summary>Fake de <see cref="IPasswordSignIn"/> configurável, evitando depender de uma
/// biblioteca de mocking para simular o SignInManager do Identity.</summary>
public sealed class FakePasswordSignIn(SignInOutcome outcomeToReturn) : IPasswordSignIn
{
    public string? LastEmail { get; private set; }
    public string? LastPassword { get; private set; }
    public bool? LastRememberMe { get; private set; }

    public Task<SignInOutcome> PasswordSignInAsync(string email, string password, bool rememberMe)
    {
        LastEmail = email;
        LastPassword = password;
        LastRememberMe = rememberMe;
        return Task.FromResult(outcomeToReturn);
    }
}

public class LoginCommandHandlerTests
{
    [Fact]
    public async Task Handle_DeveRetornarSucesso_QuandoSignInSucede()
    {
        var signIn = new FakePasswordSignIn(SignInOutcome.Succeeded);
        var handler = new LoginCommandHandler(signIn);

        var result = await handler.Handle(new LoginCommand("admin@andregas.com.br", "senha123", true), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_DeveRepassarCredenciaisParaOSignIn()
    {
        var signIn = new FakePasswordSignIn(SignInOutcome.Succeeded);
        var handler = new LoginCommandHandler(signIn);

        await handler.Handle(new LoginCommand("admin@andregas.com.br", "senha123", true), CancellationToken.None);

        Assert.Equal("admin@andregas.com.br", signIn.LastEmail);
        Assert.Equal("senha123", signIn.LastPassword);
        Assert.True(signIn.LastRememberMe);
    }

    [Fact]
    public async Task Handle_DeveRetornarFalha_QuandoCredenciaisInvalidas()
    {
        var signIn = new FakePasswordSignIn(SignInOutcome.Failed);
        var handler = new LoginCommandHandler(signIn);

        var result = await handler.Handle(new LoginCommand("admin@andregas.com.br", "errada", false), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("E-mail ou senha inválidos.", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_DeveRetornarFalha_QuandoContaBloqueada()
    {
        var signIn = new FakePasswordSignIn(SignInOutcome.LockedOut);
        var handler = new LoginCommandHandler(signIn);

        var result = await handler.Handle(new LoginCommand("admin@andregas.com.br", "senha123", false), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("bloqueada", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_DeveRetornarFalha_QuandoLoginNaoPermitido()
    {
        var signIn = new FakePasswordSignIn(SignInOutcome.NotAllowed);
        var handler = new LoginCommandHandler(signIn);

        var result = await handler.Handle(new LoginCommand("admin@andregas.com.br", "senha123", false), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.ErrorMessage);
    }
}