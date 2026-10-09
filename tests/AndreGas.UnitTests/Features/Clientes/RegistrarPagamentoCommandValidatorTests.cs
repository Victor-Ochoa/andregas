using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Clientes.Pagar;

namespace AndreGas.UnitTests.Features.Clientes;

public class RegistrarPagamentoCommandValidatorTests
{
    [Fact]
    public async Task Validate_DeveAceitarPagamentoValido()
    {
        var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        cliente.AdicionarSaldoDevedor(100m);
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var validator = new RegistrarPagamentoCommandValidator(db);
        var result = await validator.ValidateAsync(new RegistrarPagamentoCommand(cliente.Id, 40m, FormaPagamento.Dinheiro));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveRejeitarValorZeroOuNegativo()
    {
        var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var validator = new RegistrarPagamentoCommandValidator(db);
        var result = await validator.ValidateAsync(new RegistrarPagamentoCommand(cliente.Id, 0m, FormaPagamento.Dinheiro));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegistrarPagamentoCommand.Valor));
    }

    [Fact]
    public async Task Validate_DeveRejeitarFormaFiado()
    {
        var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var validator = new RegistrarPagamentoCommandValidator(db);
        var result = await validator.ValidateAsync(new RegistrarPagamentoCommand(cliente.Id, 40m, FormaPagamento.Fiado));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegistrarPagamentoCommand.FormaPagamento));
    }

    [Fact]
    public async Task Validate_DeveRejeitarValorMaiorQueSaldoDevedor()
    {
        var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        cliente.AdicionarSaldoDevedor(50m);
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var validator = new RegistrarPagamentoCommandValidator(db);
        var result = await validator.ValidateAsync(new RegistrarPagamentoCommand(cliente.Id, 60m, FormaPagamento.Pix));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegistrarPagamentoCommand.Valor));
    }
}