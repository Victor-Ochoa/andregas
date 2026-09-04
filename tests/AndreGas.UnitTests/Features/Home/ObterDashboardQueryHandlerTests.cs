using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Home;

namespace AndreGas.UnitTests.Features.Home;

public class ObterDashboardQueryHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 9, 15, 14, 0, 0, DateTimeKind.Utc);

    private static async Task<(AppDbContext db, Cliente cliente, Produto produto1, Produto produto2)> CriarBaseAsync()
    {
        var db = InMemoryDbContextFactory.Create();

        var cliente = new Cliente("Maria Souza", "11999999999", "Rua A, 10");
        var produto1 = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, estoqueMinimo: 5);
        produto1.RegistrarEntrada(2);

        var produto2 = new Produto("Água 20L", TipoProduto.Agua, 25m, 12m, 18m, estoqueMinimo: 10);
        produto2.RegistrarEntrada(6);

        db.Clientes.Add(cliente);
        db.Produtos.AddRange(produto1, produto2);
        await db.SaveChangesAsync();

        return (db, cliente, produto1, produto2);
    }

    [Fact]
    public async Task Handle_PeriodoHoje_DeveFiltrarVendasDeHojeEAgregarPorHora()
    {
        var (db, cliente, produto1, _) = await CriarBaseAsync();

        var vendaHoje = new Venda(cliente.Id, FormaPagamento.Dinheiro, new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc));
        vendaHoje.AdicionarItem(produto1.Id, 2, 100m, 60m);
        vendaHoje.AplicarDesconto(20m);

        var vendaDiaAnterior = new Venda(cliente.Id, FormaPagamento.Dinheiro, new DateTime(2026, 9, 14, 10, 0, 0, DateTimeKind.Utc));
        vendaDiaAnterior.AdicionarItem(produto1.Id, 1, 100m, 60m);

        db.Vendas.AddRange(vendaHoje, vendaDiaAnterior);
        await db.SaveChangesAsync();

        var handler = new ObterDashboardQueryHandler(db, new FixedTimeProvider(FixedNow));
        var result = await handler.Handle(new ObterDashboardQuery(PeriodoDashboard.Hoje), CancellationToken.None);

        Assert.Equal(180m, result.VendasTotal);
        Assert.Equal(1, result.VendasQuantidade);
        Assert.Equal(60m, result.LucroTotal);
        Assert.Equal(20m, result.DescontoTotal);

        var periodo = Assert.Single(result.VendasPorPeriodo);
        Assert.Equal("12h", periodo.Rotulo);
        Assert.Equal(180m, periodo.Total);
        Assert.Equal(60m, periodo.Lucro);

        Assert.Equal(2, result.ProdutosEstoqueBaixo.Count);
    }

    [Fact]
    public async Task Handle_PeriodoEsteMes_DeveAgregarPorDia()
    {
        var (db, cliente, produto1, produto2) = await CriarBaseAsync();

        var vendaHoje = new Venda(cliente.Id, FormaPagamento.Dinheiro, new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc));
        vendaHoje.AdicionarItem(produto1.Id, 2, 100m, 60m);
        vendaHoje.AplicarDesconto(20m);

        var vendaMes = new Venda(cliente.Id, FormaPagamento.Dinheiro, new DateTime(2026, 9, 5, 10, 0, 0, DateTimeKind.Utc));
        vendaMes.AdicionarItem(produto2.Id, 2, 25m, 12m);

        var vendaMesPassado = new Venda(cliente.Id, FormaPagamento.Dinheiro, new DateTime(2026, 8, 29, 9, 0, 0, DateTimeKind.Utc));
        vendaMesPassado.AdicionarItem(produto1.Id, 1, 100m, 60m);

        db.Vendas.AddRange(vendaHoje, vendaMes, vendaMesPassado);
        await db.SaveChangesAsync();

        var handler = new ObterDashboardQueryHandler(db, new FixedTimeProvider(FixedNow));
        var result = await handler.Handle(new ObterDashboardQuery(PeriodoDashboard.EsteMes), CancellationToken.None);

        Assert.Equal(230m, result.VendasTotal);
        Assert.Equal(2, result.VendasQuantidade);
        Assert.Equal(86m, result.LucroTotal);
        Assert.Equal(20m, result.DescontoTotal);

        Assert.Equal(2, result.VendasPorPeriodo.Count);
        Assert.Equal("05/09", result.VendasPorPeriodo[0].Rotulo);
        Assert.Equal(50m, result.VendasPorPeriodo[0].Total);
        Assert.Equal(26m, result.VendasPorPeriodo[0].Lucro);

        Assert.Equal("15/09", result.VendasPorPeriodo[1].Rotulo);
        Assert.Equal(180m, result.VendasPorPeriodo[1].Total);
        Assert.Equal(60m, result.VendasPorPeriodo[1].Lucro);
    }

    [Fact]
    public async Task Handle_PeriodoTudo_DeveIncluirTodasAsVendas()
    {
        var (db, cliente, produto1, produto2) = await CriarBaseAsync();

        var vendaHoje = new Venda(cliente.Id, FormaPagamento.Dinheiro, new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc));
        vendaHoje.AdicionarItem(produto1.Id, 2, 100m, 60m);
        vendaHoje.AplicarDesconto(20m);

        var vendaMes = new Venda(cliente.Id, FormaPagamento.Dinheiro, new DateTime(2026, 9, 5, 10, 0, 0, DateTimeKind.Utc));
        vendaMes.AdicionarItem(produto2.Id, 2, 25m, 12m);

        var vendaMesPassado = new Venda(cliente.Id, FormaPagamento.Dinheiro, new DateTime(2026, 8, 29, 9, 0, 0, DateTimeKind.Utc));
        vendaMesPassado.AdicionarItem(produto1.Id, 1, 100m, 60m);

        db.Vendas.AddRange(vendaHoje, vendaMes, vendaMesPassado);
        await db.SaveChangesAsync();

        var handler = new ObterDashboardQueryHandler(db, new FixedTimeProvider(FixedNow));
        var result = await handler.Handle(new ObterDashboardQuery(PeriodoDashboard.Tudo), CancellationToken.None);

        Assert.Equal(330m, result.VendasTotal);
        Assert.Equal(3, result.VendasQuantidade);
        Assert.Equal(126m, result.LucroTotal);
    }

    [Fact]
    public async Task Handle_DeveContarVendaFiadoComoVendaMasExcluirSeuLucroDosLucrosDoPeriodo()
    {
        var (db, cliente, produto, _) = await CriarBaseAsync();

        // Venda paga hoje: entra no lucro.
        var vendaPaga = new Venda(cliente.Id, FormaPagamento.Dinheiro, new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc));
        vendaPaga.AdicionarItem(produto.Id, 2, 100m, 60m);

        // Venda fiado hoje: conta como venda, mas o lucro NÃO deve entrar no lucro do período.
        var vendaFiado = new Venda(cliente.Id, FormaPagamento.Fiado, new DateTime(2026, 9, 15, 12, 30, 0, DateTimeKind.Utc));
        vendaFiado.AdicionarItem(produto.Id, 1, 100m, 60m);

        db.Vendas.AddRange(vendaPaga, vendaFiado);
        await db.SaveChangesAsync();

        var handler = new ObterDashboardQueryHandler(db, new FixedTimeProvider(FixedNow));
        var result = await handler.Handle(new ObterDashboardQuery(PeriodoDashboard.Hoje), CancellationToken.None);

        // As duas vendas contam nos totais de vendas (a fiada também é contabilizada como venda).
        Assert.Equal(300m, result.VendasTotal);
        Assert.Equal(2, result.VendasQuantidade);

        // Porém o lucro da venda fiado (40m) é excluído — só o da venda paga (80m) entra.
        Assert.Equal(80m, result.LucroTotal);

        // O gráfico conta as duas vendas no total da hora, mas o lucro exibido exclui o fiado.
        var periodo = Assert.Single(result.VendasPorPeriodo);
        Assert.Equal(300m, periodo.Total);
        Assert.Equal(80m, periodo.Lucro);
    }

    [Fact]
    public async Task Handle_DeveIncluirLucroDeVendaFiadoQuitada_NosLucrosDoPeriodo()
    {
        var (db, cliente, produto, _) = await CriarBaseAsync();

        // Venda fiado ainda aberta (não paga): lucro NÃO entra.
        var vendaFiadoAberto = new Venda(cliente.Id, FormaPagamento.Fiado, new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc));
        vendaFiadoAberto.AdicionarItem(produto.Id, 1, 100m, 60m);

        // Venda fiado já quitada (paga hoje): lucro DEVE entrar.
        var vendaFiadoQuitado = new Venda(cliente.Id, FormaPagamento.Fiado, new DateTime(2026, 9, 15, 13, 0, 0, DateTimeKind.Utc));
        vendaFiadoQuitado.AdicionarItem(produto.Id, 2, 100m, 60m);
        vendaFiadoQuitado.MarcarFiadoQuitado();

        db.Vendas.AddRange(vendaFiadoAberto, vendaFiadoQuitado);
        await db.SaveChangesAsync();

        var handler = new ObterDashboardQueryHandler(db, new FixedTimeProvider(FixedNow));
        var result = await handler.Handle(new ObterDashboardQuery(PeriodoDashboard.Hoje), CancellationToken.None);

        // As duas vendas contam nos totais de vendas.
        Assert.Equal(300m, result.VendasTotal);
        Assert.Equal(2, result.VendasQuantidade);

        // Só o lucro da fiado quitada (2 x 40 = 80m) entra; o da fiado aberta (40m) fica fora.
        Assert.Equal(80m, result.LucroTotal);
    }

    [Fact]
    public async Task Handle_DeveCalcularTotalDevedor_ComoSomaDosSaldosDosClientes()
    {
        var (db, cliente, produto, _) = await CriarBaseAsync();

        cliente.AdicionarSaldoDevedor(150m);

        var outroCliente = new Cliente("João Pereira", "11988887777", "Rua B, 20");
        outroCliente.AdicionarSaldoDevedor(40m);
        db.Clientes.Add(outroCliente);

        var venda = new Venda(cliente.Id, FormaPagamento.Dinheiro, new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc));
        venda.AdicionarItem(produto.Id, 1, 100m, 60m);
        db.Vendas.Add(venda);

        await db.SaveChangesAsync();

        var handler = new ObterDashboardQueryHandler(db, new FixedTimeProvider(FixedNow));
        var result = await handler.Handle(new ObterDashboardQuery(PeriodoDashboard.Hoje), CancellationToken.None);

        // TotalDevedor independe do período — é a soma de todos os saldos devedores em aberto.
        Assert.Equal(190m, result.TotalDevedor);
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
