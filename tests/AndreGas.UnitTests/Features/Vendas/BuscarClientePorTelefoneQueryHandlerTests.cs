using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Vendas.NovaVenda;

namespace AndreGas.UnitTests.Features.Vendas;

public class BuscarClientePorTelefoneQueryHandlerTests
{
    [Fact]
    public async Task Handle_DeveRetornarUltimoValorDeEntrega_DoCliente()
    {
        var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        // Venda mais antiga com entrega 10; mais recente com entrega 20.
        var vendaAntiga = new Venda(cliente.Id, FormaPagamento.Dinheiro, new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc));
        vendaAntiga.DefinirValorEntrega(10m);
        db.Vendas.Add(vendaAntiga);

        var vendaRecente = new Venda(cliente.Id, FormaPagamento.Dinheiro, new DateTime(2026, 10, 2, 10, 0, 0, DateTimeKind.Utc));
        vendaRecente.DefinirValorEntrega(20m);
        db.Vendas.Add(vendaRecente);
        await db.SaveChangesAsync();

        var handler = new BuscarClientePorTelefoneQueryHandler(db);
        var result = await handler.Handle(new BuscarClientePorTelefoneQuery("11988887777"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(20m, result!.UltimaValorEntrega);
    }

    [Fact]
    public async Task Handle_DeveRetornarZero_QuandoClienteNaoTemVendas()
    {
        var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var handler = new BuscarClientePorTelefoneQueryHandler(db);
        var result = await handler.Handle(new BuscarClientePorTelefoneQuery("11988887777"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(0m, result!.UltimaValorEntrega);
    }

    [Fact]
    public async Task Handle_DeveRetornarZero_QuandoUltimaVendaTemEntregaZero()
    {
        var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        // Última venda (mais recente) tem entrega 0 -> sugere 0, mesmo a antiga tendo 10.
        var vendaAntiga = new Venda(cliente.Id, FormaPagamento.Dinheiro, new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc));
        vendaAntiga.DefinirValorEntrega(10m);
        db.Vendas.Add(vendaAntiga);

        var vendaRecente = new Venda(cliente.Id, FormaPagamento.Dinheiro, new DateTime(2026, 10, 2, 10, 0, 0, DateTimeKind.Utc));
        db.Vendas.Add(vendaRecente);
        await db.SaveChangesAsync();

        var handler = new BuscarClientePorTelefoneQueryHandler(db);
        var result = await handler.Handle(new BuscarClientePorTelefoneQuery("11988887777"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(0m, result!.UltimaValorEntrega);
    }
}