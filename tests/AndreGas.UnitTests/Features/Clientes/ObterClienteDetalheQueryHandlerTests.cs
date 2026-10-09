using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Clientes.Detalhe;

namespace AndreGas.UnitTests.Features.Clientes;

public class ObterClienteDetalheQueryHandlerTests
{
    [Fact]
    public async Task Handle_DeveRetornarNull_QuandoClienteNaoExiste()
    {
        using var db = InMemoryDbContextFactory.Create();
        var handler = new ObterClienteDetalheQueryHandler(db);

        var result = await handler.Handle(new ObterClienteDetalheQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_DeveRetornarDadosDoClienteESaldoDevedor()
    {
        using var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        cliente.AdicionarSaldoDevedor(150m);
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var handler = new ObterClienteDetalheQueryHandler(db);

        var result = await handler.Handle(new ObterClienteDetalheQuery(cliente.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Maria Souza", result!.Nome);
        Assert.Equal(150m, result.SaldoDevedor);
    }

    [Fact]
    public async Task Handle_DeveIncluirHistoricoDeComprasComValorELucro()
    {
        using var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);

        var venda = new Venda(cliente.Id, FormaPagamento.Dinheiro);
        venda.AdicionarItem(Guid.NewGuid(), quantidade: 2, precoUnitario: 100m, precoCustoUnitario: 60m);
        db.Vendas.Add(venda);

        await db.SaveChangesAsync();

        var handler = new ObterClienteDetalheQueryHandler(db);

        var result = await handler.Handle(new ObterClienteDetalheQuery(cliente.Id), CancellationToken.None);

        Assert.NotNull(result);
        var item = Assert.Single(result!.HistoricoDeCompras);
        Assert.Equal(200m, item.ValorTotal);
        Assert.Equal(80m, item.LucroTotal);
        Assert.Equal(FormaPagamento.Dinheiro, item.FormaPagamento);
        Assert.Empty(result.HistoricoDePagamentos);
    }

    [Fact]
    public async Task Handle_DeveIncluirHistoricoDePagamentos()
    {
        using var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        db.Pagamentos.Add(new Pagamento(cliente.Id, 50m, FormaPagamento.Dinheiro, data: new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc)));

        var venda = new Venda(cliente.Id, FormaPagamento.Fiado);
        venda.AdicionarItem(Guid.NewGuid(), quantidade: 1, precoUnitario: 100m, precoCustoUnitario: 60m);
        db.Vendas.Add(venda);

        await db.SaveChangesAsync();

        var handler = new ObterClienteDetalheQueryHandler(db);

        var result = await handler.Handle(new ObterClienteDetalheQuery(cliente.Id), CancellationToken.None);

        Assert.NotNull(result);
        var pagamento = Assert.Single(result!.HistoricoDePagamentos);
        Assert.Equal(50m, pagamento.Valor);
        Assert.Equal(FormaPagamento.Dinheiro, pagamento.FormaPagamento);
        Assert.Single(result.HistoricoDeCompras);
    }

    [Fact]
    public async Task Handle_DeveIncluirValorEntregaNoHistoricoDeCompras()
    {
        using var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);

        var venda = new Venda(cliente.Id, FormaPagamento.Dinheiro);
        venda.AdicionarItem(Guid.NewGuid(), quantidade: 2, precoUnitario: 100m, precoCustoUnitario: 60m);
        venda.DefinirValorEntrega(15m);
        db.Vendas.Add(venda);

        await db.SaveChangesAsync();

        var handler = new ObterClienteDetalheQueryHandler(db);

        var result = await handler.Handle(new ObterClienteDetalheQuery(cliente.Id), CancellationToken.None);

        Assert.NotNull(result);
        var item = Assert.Single(result!.HistoricoDeCompras);
        Assert.Equal(215m, item.ValorTotal); // 200 + 15 de entrega
        Assert.Equal(15m, item.ValorEntrega);
        Assert.Equal(80m, item.LucroTotal);  // entrega não altera o lucro
    }

    [Fact]
    public async Task Handle_DeveExcluirVendaExcluida_DoHistoricoDeCompras()
    {
        using var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);

        var vendaAtiva = new Venda(cliente.Id, FormaPagamento.Dinheiro);
        vendaAtiva.AdicionarItem(Guid.NewGuid(), quantidade: 1, precoUnitario: 100m, precoCustoUnitario: 60m);
        db.Vendas.Add(vendaAtiva);

        var vendaExcluida = new Venda(cliente.Id, FormaPagamento.Fiado);
        vendaExcluida.AdicionarItem(Guid.NewGuid(), quantidade: 2, precoUnitario: 100m, precoCustoUnitario: 60m);
        vendaExcluida.Excluir();
        db.Vendas.Add(vendaExcluida);

        await db.SaveChangesAsync();

        var handler = new ObterClienteDetalheQueryHandler(db);
        var result = await handler.Handle(new ObterClienteDetalheQuery(cliente.Id), CancellationToken.None);

        Assert.NotNull(result);
        var item = Assert.Single(result!.HistoricoDeCompras);
        Assert.Equal(FormaPagamento.Dinheiro, item.FormaPagamento);
    }
}