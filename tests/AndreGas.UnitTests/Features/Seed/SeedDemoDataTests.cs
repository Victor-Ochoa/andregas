using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Seed;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.UnitTests.Features.Seed;

public class SeedDemoDataTests
{
    // Data fixa para o teste: 15/09/2026 14:00 UTC.
    private static readonly DateTime FixedNow = new(2026, 9, 15, 14, 0, 0, DateTimeKind.Utc);
    private static readonly TimeProvider FixedTime = new FixedTimeProvider(FixedNow);

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    [Fact]
    public async Task SeedAsync_DeveCriarProdutosClientesVendasEPagamentos()
    {
        var db = InMemoryDbContextFactory.Create();

        await SeedDemoData.SeedAsync(db, FixedTime);

        // Produtos com estoque cadastrado.
        var produtos = await db.Produtos.AsNoTracking().ToListAsync();
        Assert.NotEmpty(produtos);
        Assert.All(produtos, p => Assert.True(p.QuantidadeEstoque > 0, $"{p.Nome} sem estoque"));
        Assert.Contains(produtos, p => p.Tipo == TipoProduto.Gas);
        Assert.Contains(produtos, p => p.Tipo == TipoProduto.Agua);
        Assert.Contains(produtos, p => p.Tipo == TipoProduto.Outros);

        // Clientes variados.
        var clientes = await db.Clientes.AsNoTracking().ToListAsync();
        Assert.NotEmpty(clientes);
        Assert.All(clientes, c => Assert.False(string.IsNullOrWhiteSpace(c.Telefone)));

        // Vendas cobrindo TODAS as formas de pagamento.
        var vendas = await db.Vendas.AsNoTracking().Include(v => v.Itens).ToListAsync();
        Assert.NotEmpty(vendas);
        foreach (var forma in Enum.GetValues<FormaPagamento>())
            Assert.Contains(vendas, v => v.FormaPagamento == forma);

        // Ao menos uma venda fiado aberta e uma fiado quitada.
        Assert.Contains(vendas, v => v.Status == VendaStatus.FiadoAberto);
        Assert.Contains(vendas, v => v.Status == VendaStatus.FiadoQuitado);

        // Todas as vendas com data anterior a hoje.
        Assert.All(vendas, v => Assert.True(v.DataHora < FixedNow));

        // Vendas não-fiado geram Pagamento.
        var pagamentos = await db.Pagamentos.AsNoTracking().ToListAsync();
        Assert.NotEmpty(pagamentos);
        var vendasPagas = vendas.Where(v => v.FormaPagamento != FormaPagamento.Fiado).ToList();
        Assert.True(pagamentos.Count >= vendasPagas.Count);

        // O pagamento de uma venda paga no ato deve ter a MESMA data/hora da venda (não "agora"),
        // para que o pagamento "bata" com a venda nos dashboards por período.
        foreach (var venda in vendasPagas)
        {
            var pagamento = pagamentos.SingleOrDefault(p => p.ClienteId == venda.ClienteId
                && p.Valor == venda.ValorTotal
                && p.FormaPagamento == venda.FormaPagamento);
            Assert.NotNull(pagamento);
            Assert.Equal(venda.DataHora, pagamento!.Data);
        }

        // Movimentações de estoque (entrada + saída por venda).
        var movs = await db.MovimentacoesEstoque.AsNoTracking().ToListAsync();
        Assert.Contains(movs, m => m.Tipo == TipoMovimentacaoEstoque.Entrada);
        Assert.Contains(movs, m => m.Tipo == TipoMovimentacaoEstoque.Saida);

        // Saldo devedor: cliente com fiado aberto tem saldo > 0.
        Assert.Contains(clientes, c => c.SaldoDevedor > 0);
    }

    [Fact]
    public async Task SeedAsync_DeveSerIdempotente_NaoDuplicarDadosAoExecutarDuasVezes()
    {
        var db = InMemoryDbContextFactory.Create();

        await SeedDemoData.SeedAsync(db, FixedTime);
        var qtdClientes = await db.Clientes.CountAsync();
        var qtdProdutos = await db.Produtos.CountAsync();
        var qtdVendas = await db.Vendas.CountAsync();

        await SeedDemoData.SeedAsync(db, FixedTime);

        Assert.Equal(qtdClientes, await db.Clientes.CountAsync());
        Assert.Equal(qtdProdutos, await db.Produtos.CountAsync());
        Assert.Equal(qtdVendas, await db.Vendas.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_QuandoBancoJaTemProdutos_DevePularSemDuplicar()
    {
        var db = InMemoryDbContextFactory.Create();
        db.Produtos.Add(new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m));
        await db.SaveChangesAsync();

        await SeedDemoData.SeedAsync(db, FixedTime);

        // Apenas o produto pré-existente deve permanecer (sem duplicar o seed).
        Assert.Single(await db.Produtos.ToListAsync());
    }

    [Fact]
    public async Task SeedAsync_DeveCriarVendasComValorDeEntrega()
    {
        var db = InMemoryDbContextFactory.Create();

        await SeedDemoData.SeedAsync(db, FixedTime);

        var vendasComEntrega = await db.Vendas.AsNoTracking()
            .Where(v => v.ValorEntrega > 0)
            .ToListAsync();

        // O seed aplica entrega em algumas vendas (demonstração do faturamento).
        Assert.NotEmpty(vendasComEntrega);
    }

    [Fact]
    public async Task SeedAsync_DeveCriarRecorrenciasDeRecompra_ParaProdutosComAvisarRecompra()
    {
        var db = InMemoryDbContextFactory.Create();

        await SeedDemoData.SeedAsync(db, FixedTime);

        // Produtos do seed marcados com AvisarRecompra devem ter recorrência para clientes que compraram.
        var produtosComAviso = await db.Produtos.AsNoTracking()
            .Where(p => p.AvisarRecompra)
            .ToListAsync();
        Assert.NotEmpty(produtosComAviso);

        foreach (var produto in produtosComAviso)
        {
            // Ao menos um cliente tem recorrência para este produto, com intervalo aprendido.
            var recorrencia = await db.RecorrenciasCompra.AsNoTracking()
                .FirstOrDefaultAsync(r => r.ProdutoId == produto.Id);
            Assert.NotNull(recorrencia);
            Assert.True(recorrencia!.IntervaloMedioDias > 0, $"{produto.Nome} sem intervalo de recompra.");
        }

        // Maria + Botijão 13kg: cliente com múltiplas vendas do produto → recorrência aprendida
        // (intervalo > 0 e mais de 1 compra, pois o seed espaça as vendas no tempo).
        var maria = await db.Clientes.AsNoTracking().SingleOrDefaultAsync(c => c.Nome == "Maria Souza");
        Assert.NotNull(maria);
        var botijao = await db.Produtos.AsNoTracking().SingleOrDefaultAsync(p => p.Nome == "Botijão Gás 13kg");
        Assert.NotNull(botijao);
        var recMaria = await db.RecorrenciasCompra.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ClienteId == maria!.Id && r.ProdutoId == botijao!.Id);
        Assert.NotNull(recMaria);
        Assert.True(recMaria!.IntervaloMedioDias > 0);
        Assert.True(recMaria.QuantidadeCompras >= 2);
    }
}