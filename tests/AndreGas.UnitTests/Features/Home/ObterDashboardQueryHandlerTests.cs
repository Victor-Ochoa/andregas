using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Home;

namespace AndreGas.UnitTests.Features.Home;

public class ObterDashboardQueryHandlerTests
{
    [Fact]
    public async Task Handle_DeveCalcularTotaisDoMesEDiaEListarProdutosEmEstoqueBaixo()
    {
        using var db = InMemoryDbContextFactory.Create();

        var cliente = new Cliente("Maria Souza", "11999999999", "Rua A, 10");
        var produto1 = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, estoqueMinimo: 5);
        produto1.RegistrarEntrada(2);

        var produto2 = new Produto("Água 20L", TipoProduto.Agua, 25m, 12m, 18m, estoqueMinimo: 10);
        produto2.RegistrarEntrada(6);

        db.Clientes.Add(cliente);
        db.Produtos.AddRange(produto1, produto2);
        await db.SaveChangesAsync();

        var vendaHoje = new Venda(cliente.Id, FormaPagamento.Dinheiro, new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc));
        vendaHoje.AdicionarItem(produto1.Id, 2, 100m, 60m);
        vendaHoje.AplicarDesconto(20m);

        var vendaMes = new Venda(cliente.Id, FormaPagamento.Dinheiro, new DateTime(2026, 9, 05, 10, 0, 0, DateTimeKind.Utc));
        vendaMes.AdicionarItem(produto2.Id, 2, 25m, 12m);

        var vendaMesPassado = new Venda(cliente.Id, FormaPagamento.Dinheiro, new DateTime(2026, 8, 29, 9, 0, 0, DateTimeKind.Utc));
        vendaMesPassado.AdicionarItem(produto1.Id, 1, 100m, 60m);

        db.Vendas.AddRange(vendaHoje, vendaMes, vendaMesPassado);
        await db.SaveChangesAsync();

        var handler = new ObterDashboardQueryHandler(db, new FixedTimeProvider(new DateTime(2026, 9, 15, 14, 0, 0, DateTimeKind.Utc)));

        var result = await handler.Handle(new ObterDashboardQuery(), CancellationToken.None);

        Assert.Equal(180m, result.VendasHojeTotal);
        Assert.Equal(1, result.VendasHojeQuantidade);
        Assert.Equal(230m, result.VendasMesTotal);
        Assert.Equal(2, result.VendasMesQuantidade);
        Assert.Equal(60m, result.LucroHoje);
        Assert.Equal(86m, result.LucroMes);

        Assert.Equal(2, result.VendasPorDiaNoMes.Count);
        Assert.Equal(new DateOnly(2026, 9, 5), result.VendasPorDiaNoMes[0].Data);
        Assert.Equal(50m, result.VendasPorDiaNoMes[0].Total);
        Assert.Equal(26m, result.VendasPorDiaNoMes[0].Lucro);

        Assert.Equal(new DateOnly(2026, 9, 15), result.VendasPorDiaNoMes[1].Data);
        Assert.Equal(180m, result.VendasPorDiaNoMes[1].Total);
        Assert.Equal(60m, result.VendasPorDiaNoMes[1].Lucro);

        var produtos = result.ProdutosEstoqueBaixo;
        Assert.Equal(2, produtos.Count);
        Assert.Equal("Botijão 13kg", produtos[0].Nome);
        Assert.Equal("Água 20L", produtos[1].Nome);
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
