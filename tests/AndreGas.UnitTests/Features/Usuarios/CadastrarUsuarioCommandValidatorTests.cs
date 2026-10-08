using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Usuarios;
using AndreGas.Web.Features.Usuarios.Cadastrar;
using FluentValidation.TestHelper;

namespace AndreGas.UnitTests.Features.Usuarios;

public class CadastrarUsuarioCommandValidatorTests
{
    private static CadastrarUsuarioCommand ComandoValido() =>
        new("Maria Souza", "maria@andregas.com.br", UsuarioPapel.Vendedor, "senha123", "senha123");

    [Fact]
    public async Task Validator_DeveSerValido_ParaComandoCorreto()
    {
        using var db = InMemoryDbContextFactory.Create();
        var validator = new CadastrarUsuarioCommandValidator(db);

        var result = await validator.ValidateAsync(ComandoValido());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validator_DeveRejeitar_QuandoNomeVazio()
    {
        using var db = InMemoryDbContextFactory.Create();
        var validator = new CadastrarUsuarioCommandValidator(db);

        var result = await validator.ValidateAsync(ComandoValido() with { Nome = "" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CadastrarUsuarioCommand.Nome));
    }

    [Fact]
    public async Task Validator_DeveRejeitar_QuandoEmailInvalido()
    {
        using var db = InMemoryDbContextFactory.Create();
        var validator = new CadastrarUsuarioCommandValidator(db);

        var result = await validator.ValidateAsync(ComandoValido() with { Email = "invalido" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CadastrarUsuarioCommand.Email));
    }

    [Fact]
    public async Task Validator_DeveRejeitar_QuandoSenhaCurta()
    {
        using var db = InMemoryDbContextFactory.Create();
        var validator = new CadastrarUsuarioCommandValidator(db);

        var result = await validator.ValidateAsync(ComandoValido() with { Senha = "123", ConfirmarSenha = "123" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CadastrarUsuarioCommand.Senha));
    }

    [Fact]
    public async Task Validator_DeveRejeitar_QuandoSenhasNaoConferem()
    {
        using var db = InMemoryDbContextFactory.Create();
        var validator = new CadastrarUsuarioCommandValidator(db);

        var result = await validator.ValidateAsync(ComandoValido() with { ConfirmarSenha = "diferente" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CadastrarUsuarioCommand.ConfirmarSenha));
    }

    [Fact]
    public async Task Validator_DeveRejeitar_QuandoEmailJaCadastrado()
    {
        using var db = InMemoryDbContextFactory.Create();
        const string emailExistente = "existente@andregas.com.br";
        db.Users.Add(new AndreGas.Infrastructure.Identity.ApplicationUser
        {
            UserName = emailExistente,
            Email = emailExistente,
        });
        await db.SaveChangesAsync();

        var validator = new CadastrarUsuarioCommandValidator(db);

        var result = await validator.ValidateAsync(ComandoValido() with { Email = emailExistente });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CadastrarUsuarioCommand.Email));
    }
}