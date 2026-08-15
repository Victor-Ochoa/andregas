using AndreGas.Domain.Entities;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Clientes.Cadastrar;

namespace AndreGas.UnitTests.Features.Clientes;

public class CadastrarClienteCommandValidatorTests
{
    [Fact]
    public async Task Validate_DeveSerValido_QuandoDadosCorretosETelefoneNovo()
    {
        using var db = InMemoryDbContextFactory.Create();
        var validator = new CadastrarClienteCommandValidator(db);

        var result = await validator.ValidateAsync(new CadastrarClienteCommand("Maria Souza", "11988887777", "Rua A, 1"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoTelefoneJaCadastrado()
    {
        using var db = InMemoryDbContextFactory.Create();
        db.Clientes.Add(new Cliente("João Silva", "11988887777", "Rua B, 2"));
        await db.SaveChangesAsync();

        var validator = new CadastrarClienteCommandValidator(db);

        var result = await validator.ValidateAsync(new CadastrarClienteCommand("Maria Souza", "(11) 98888-7777", "Rua A, 1"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CadastrarClienteCommand.Telefone));
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoNomeVazio()
    {
        using var db = InMemoryDbContextFactory.Create();
        var validator = new CadastrarClienteCommandValidator(db);

        var result = await validator.ValidateAsync(new CadastrarClienteCommand("", "11988887777", "Rua A, 1"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CadastrarClienteCommand.Nome));
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoEnderecoVazio()
    {
        using var db = InMemoryDbContextFactory.Create();
        var validator = new CadastrarClienteCommandValidator(db);

        var result = await validator.ValidateAsync(new CadastrarClienteCommand("Maria Souza", "11988887777", ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CadastrarClienteCommand.Endereco));
    }
}
