using AndreGas.Domain.Entities;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Clientes.Detalhe;

namespace AndreGas.UnitTests.Features.Clientes;

public class AtualizarClienteCommandValidatorTests
{
    [Fact]
    public async Task Validate_DeveSerValido_QuandoTelefoneNaoMudou()
    {
        using var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var validator = new AtualizarClienteCommandValidator(db);

        var result = await validator.ValidateAsync(new AtualizarClienteCommand(cliente.Id, "Maria S. Oliveira", "11988887777", "Rua B, 2"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoTelefoneJaPertenceAOutroCliente()
    {
        using var db = InMemoryDbContextFactory.Create();
        var cliente1 = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        var cliente2 = new Cliente("João Silva", "11999998888", "Rua B, 2");
        db.Clientes.AddRange(cliente1, cliente2);
        await db.SaveChangesAsync();

        var validator = new AtualizarClienteCommandValidator(db);

        var result = await validator.ValidateAsync(new AtualizarClienteCommand(cliente2.Id, "João Silva", "11988887777", "Rua B, 2"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AtualizarClienteCommand.Telefone));
    }
}