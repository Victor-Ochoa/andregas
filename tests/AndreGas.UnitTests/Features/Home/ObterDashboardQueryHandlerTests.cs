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

    [Fact]
    public async Task Handle_DeveContarVendaFiadoComoVendaMasExcluirSeuLucroDosLucrosDoDiaEMes()
    {
        using var db = InMemoryDbContextFactory.Create();

        var cliente = new Cliente("João Pereira", "11988887777", "Rua B, 20");
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, estoqueMinimo: 5);
        produto.RegistrarEntrada(5);

        db.Clientes.Add(cliente);
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();

        // Venda paga hoje: entra no lucro.
        var vendaPaga = new Venda(cliente.Id, FormaPagamento.Dinheiro, new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc));
        vendaPaga.AdicionarItem(produto.Id, 2, 100m, 60m);

        // Venda fiado hoje: conta como venda, mas o lucro NÃO deve entrar no lucro do dia/mês.
        var vendaFiado = new Venda(cliente.Id, FormaPagamento.Fiado, new DateTime(2026, 9, 15, 13, 0, 0, DateTimeKind.Utc));
        vendaFiado.AdicionarItem(produto.Id, 1, 100m, 60m);

        db.Vendas.AddRange(vendaPaga, vendaFiado);
        await db.SaveChangesAsync();

        var handler = new ObterDashboardQueryHandler(db, new FixedTimeProvider(new DateTime(2026, 9, 15, 14, 0, 0, DateTimeKind.Utc)));

        var result = await handler.Handle(new ObterDashboardQuery(), CancellationToken.None);

        // As duas vendas contam nos totais de vendas (a fiada também é contabilizada como venda).
        Assert.Equal(300m, result.VendasHojeTotal);
        Assert.Equal(2, result.VendasHojeQuantidade);
        Assert.Equal(300m, result.VendasMesTotal);
        Assert.Equal(2, result.VendasMesQuantidade);

        // Porém o lucro da venda fiado (40m) é excluído — só o da venda paga (80m) entra.
        Assert.Equal(80m, result.LucroHoje);
        Assert.Equal(80m, result.LucroMes);

        // O gráfico conta as duas vendas no total do dia, mas o lucro exibido exclui o fiado.
        Assert.Single(result.VendasPorDiaNoMes);
        Assert.Equal(300m, result.VendasPorDiaNoMes[0].Total);
        Assert.Equal(80m, result.VendasPorDiaNoMes[0].Lucro);
    }

    [Fact]
    public async Task Handle_DeveIncluirLucroDeVendaFiadoQuitada_NosLucrosDoDiaEMes()
    {
        using var db = InMemoryDbContextFactory.Create();

        var cliente = new Cliente("João Pereira", "11988887777", "Rua B, 20");
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, estoqueMinimo: 5);
        produto.RegistrarEntrada(5);

        db.Clientes.Add(cliente);
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();

        // Venda fiado ainda aberta (não paga): lucro NÃO entra.
        var vendaFiadoAberto = new Venda(cliente.Id, FormaPagamento.Fiado, new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc));
        vendaFiadoAberto.AdicionarItem(produto.Id, 1, 100m, 60m);

        // Venda fiado já quitada (paga hoje): lucro DEVE entrar.
        var vendaFiadoQuitado = new Venda(cliente.Id, FormaPagamento.Fiado, new DateTime(2026, 9, 15, 13, 0, 0, DateTimeKind.Utc));
        vendaFiadoQuitado.AdicionarItem(produto.Id, 2, 100m, 60m);
        vendaFiadoQuitado.MarcarFiadoQuitado();

        db.Vendas.AddRange(vendaFiadoAberto, vendaFiadoQuitado);
        await db.SaveChangesAsync();

        var handler = new ObterDashboardQueryHandler(db, new FixedTimeProvider(new DateTime(2026, 9, 15, 14, 0, 0, DateTimeKind.Utc)));

        var result = await handler.Handle(new ObterDashboardQuery(), CancellationToken.None);

        // As duas vendas contam nos totais de vendas.
        Assert.Equal(300m, result.VendasHojeTotal);
        Assert.Equal(2, result.VendasHojeQuantidade);

        // Só o lucro da fiado quitada (2 x 40 = 80m) entra; o da fiado aberta (40m) fica fora.
        Assert.Equal(80m, result.LucroHoje);
        Assert.Equal(80m, result.LucroMes);
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
