using AndreGas.Web.Features.Auth.Login;

namespace AndreGas.UnitTests.Features.Auth.Login;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void Validate_DeveSerValido_QuandoEmailESenhaPreenchidos()
    {
        var result = _validator.Validate(new LoginCommand("admin@andregas.com.br", "senha123", false));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "senha123")]
    [InlineData("nao-e-email", "senha123")]
    public void Validate_DeveSerInvalido_QuandoEmailVazioOuMalFormado(string email, string senha)
    {
        var result = _validator.Validate(new LoginCommand(email, senha, false));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginCommand.Email));
    }

    [Fact]
    public void Validate_DeveSerInvalido_QuandoSenhaVazia()
    {
        var result = _validator.Validate(new LoginCommand("admin@andregas.com.br", "", false));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginCommand.Password));
    }
}
