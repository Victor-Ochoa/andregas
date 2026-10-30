using RevendaApp.Domain.Entities;
using RevendaApp.Domain.Enums;
using RevendaApp.Infrastructure;
using RevendaApp.UnitTests.TestHelpers;
using RevendaApp.Web.Common;
using RevendaApp.Web.Features.Clientes.Detalhe;

namespace RevendaApp.UnitTests.Features.Clientes;

public class ObterClienteDetalheQueryHandlerTests
{
    [Fact]
    public async Task Handle_DeveRetornarNull_QuandoClienteNaoExiste()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var handler = new ObterClienteDetalheQueryHandler(db, TenantTeste.Contexto());

        var result = await handler.Handle(new ObterClienteDetalheQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_DeveRetornarDadosDoClienteESaldoDevedor()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        cliente.AdicionarSaldoDevedor(150m);
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var handler = new ObterClienteDetalheQueryHandler(db, TenantTeste.Contexto());

        var result = await handler.Handle(new ObterClienteDetalheQuery(cliente.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Maria Souza", result!.Nome);
        Assert.Equal(150m, result.SaldoDevedor);
    }

    [Fact]
    public async Task Handle_DeveIncluirHistoricoDeComprasComValorELucro()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);

        var venda = new Venda(cliente.Id, FormaPagamento.Dinheiro);
        venda.AdicionarItem(Guid.NewGuid(), quantidade: 2, precoUnitario: 100m, precoCustoUnitario: 60m);
        db.Vendas.Add(venda);

        await db.SaveChangesAsync();

        var handler = new ObterClienteDetalheQueryHandler(db, TenantTeste.Contexto());

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
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        db.Pagamentos.Add(new Pagamento(cliente.Id, 50m, FormaPagamento.Dinheiro, data: new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc)));

        var venda = new Venda(cliente.Id, FormaPagamento.Fiado);
        venda.AdicionarItem(Guid.NewGuid(), quantidade: 1, precoUnitario: 100m, precoCustoUnitario: 60m);
        db.Vendas.Add(venda);

        await db.SaveChangesAsync();

        var handler = new ObterClienteDetalheQueryHandler(db, TenantTeste.Contexto());

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
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);

        var venda = new Venda(cliente.Id, FormaPagamento.Dinheiro);
        venda.AdicionarItem(Guid.NewGuid(), quantidade: 2, precoUnitario: 100m, precoCustoUnitario: 60m);
        venda.DefinirValorEntrega(15m);
        db.Vendas.Add(venda);

        await db.SaveChangesAsync();

        var handler = new ObterClienteDetalheQueryHandler(db, TenantTeste.Contexto());

        var result = await handler.Handle(new ObterClienteDetalheQuery(cliente.Id), CancellationToken.None);

        Assert.NotNull(result);
        var item = Assert.Single(result!.HistoricoDeCompras);
        Assert.Equal(215m, item.ValorTotal); // 200 + 15 de entrega
        Assert.Equal(15m, item.ValorEntrega);
        Assert.Equal(80m, item.LucroTotal);  // entrega não altera o lucro
    }

    [Fact]
    public async Task Handle_DeveAgregarProdutosComprados_PorProduto()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        var gas = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m);
        var agua = new Produto("Água 20L", TipoProduto.Agua, 25m, 12m, 18m);
        db.Clientes.Add(cliente);
        db.Produtos.AddRange(gas, agua);

        // 2 vendas com gas (2 e 3 un) e 1 com agua (1 un) → total gas=5, agua=1
        var v1 = new Venda(cliente.Id, FormaPagamento.Dinheiro);
        v1.AdicionarItem(gas.Id, 2, 100m, 60m);
        db.Vendas.Add(v1);

        var v2 = new Venda(cliente.Id, FormaPagamento.Dinheiro);
        v2.AdicionarItem(gas.Id, 3, 100m, 60m);
        v2.AdicionarItem(agua.Id, 1, 25m, 12m);
        db.Vendas.Add(v2);

        await db.SaveChangesAsync();

        var handler = new ObterClienteDetalheQueryHandler(db, TenantTeste.Contexto());
        var result = await handler.Handle(new ObterClienteDetalheQuery(cliente.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result!.ProdutosComprados.Count);
        var gasItem = result.ProdutosComprados.Single(p => p.ProdutoNome == "Botijão 13kg");
        Assert.Equal(5, gasItem.QuantidadeTotal);
        var aguaItem = result.ProdutosComprados.Single(p => p.ProdutoNome == "Água 20L");
        Assert.Equal(1, aguaItem.QuantidadeTotal);
    }

    [Fact]
    public async Task Handle_DeveExcluirVendaExcluida_DoHistoricoDeCompras()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
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

        var handler = new ObterClienteDetalheQueryHandler(db, TenantTeste.Contexto());
        var result = await handler.Handle(new ObterClienteDetalheQuery(cliente.Id), CancellationToken.None);

        Assert.NotNull(result);
        var item = Assert.Single(result!.HistoricoDeCompras);
        Assert.Equal(FormaPagamento.Dinheiro, item.FormaPagamento);
    }

    [Fact]
    public async Task Handle_DeveIncluirRecompras_ComDiasDeRecompraAtual()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var cliente = new Cliente("Maria Souza", "11999999999", "Rua A, 10");
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5,
            avisarRecompra: true, diasPadraoRecompra: 45);
        produto.RegistrarEntrada(10);
        db.Clientes.Add(cliente);
        db.Produtos.Add(produto);

        // Compra há 60 dias (hoje 15/09; comprou 15/07). Intervalo aprendido = 45 (default de 1 compra).
        var venda = new Venda(cliente.Id, FormaPagamento.Dinheiro, new DateTime(2026, 7, 17, 14, 0, 0, DateTimeKind.Utc));
        venda.AdicionarItem(produto.Id, 1, 100m, 60m);
        db.Vendas.Add(venda);
        await db.SaveChangesAsync();

        // Popula a recorrência (a query guarda o aprendizado em RecorrenciaCompra).
        await RecalcularEPersistirAsync(db, cliente.Id, produto.Id);

        var handler = CriarHandler(db, new DateTime(2026, 9, 15, 14, 0, 0, DateTimeKind.Utc));
        var result = await handler.Handle(new ObterClienteDetalheQuery(cliente.Id), CancellationToken.None);

        Assert.NotNull(result);
        var recompra = Assert.Single(result!.Recompras);
        Assert.Equal(produto.Id, recompra.ProdutoId);
        Assert.Equal("Botijão 13kg", recompra.ProdutoNome);
        Assert.Equal(45m, recompra.IntervaloMedioDias);
        Assert.Equal(1, recompra.QuantidadeCompras);
        // DataPrevista = última compra (BRT) + 45 → hoje (15/09) já passou → dias decorridos > 0.
        Assert.True(recompra.DataPrevista <= DateTime.Today, "DataPrevista deveria estar vencida");
        Assert.True(recompra.DiasDecorridos > 0, "Deveria haver dias decorridos desde a data prevista");
    }

    [Fact]
    public async Task Handle_ClienteSemRecompras_DeveTerListaVazia()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var cliente = new Cliente("Maria Souza", "11999999999", "Rua A, 10");
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var handler = CriarHandler(db, new DateTime(2026, 9, 15, 14, 0, 0, DateTimeKind.Utc));
        var result = await handler.Handle(new ObterClienteDetalheQuery(cliente.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result!.Recompras);
    }

    private static async Task RecalcularEPersistirAsync(AppDbContext db, Guid clienteId, Guid produtoId)
    {
        await RecorrenciaCompraAprendizado.RecalcularAsync(db, clienteId, produtoId, CancellationToken.None);
        await db.SaveChangesAsync();
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    private static ObterClienteDetalheQueryHandler CriarHandler(AppDbContext db, DateTime utcNow) =>
        new(db, TenantTeste.Contexto(), new FixedTimeProvider(utcNow));

    [Fact]
    public async Task Handle_DeveIncluirEmprestimosEmAbertoDoCliente()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, controlaVasilhame: true);
        db.Clientes.Add(cliente);
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();

        db.EmprestimosVasilhames.Add(new EmprestimoVasilhame(cliente.Id, produto.Id, 3, new DateTime(2026, 1, 5, 10, 0, 0, DateTimeKind.Utc)));
        var quitado = new EmprestimoVasilhame(cliente.Id, produto.Id, 2, new DateTime(2026, 1, 7, 10, 0, 0, DateTimeKind.Utc));
        quitado.RegistrarDevolucao(2); // quitado → fora da lista
        db.EmprestimosVasilhames.Add(quitado);
        await db.SaveChangesAsync();

        var handler = new ObterClienteDetalheQueryHandler(db, TenantTeste.Contexto());
        var result = await handler.Handle(new ObterClienteDetalheQuery(cliente.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result!.HistoricoDeCompras);
        Assert.Empty(result.HistoricoDePagamentos);
        var emprestimo = Assert.Single(result.Emprestimos);
        Assert.Equal(produto.Id, emprestimo.ProdutoId);
        Assert.Equal("Botijão 13kg", emprestimo.ProdutoNome);
        Assert.Equal(3, emprestimo.Quantidade);
    }

    [Fact]
    public async Task Handle_ClienteSemEmprestimos_DeveTerListaVaziaDeEmprestimos()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var handler = new ObterClienteDetalheQueryHandler(db, TenantTeste.Contexto());
        var result = await handler.Handle(new ObterClienteDetalheQuery(cliente.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result!.Emprestimos);
    }
}