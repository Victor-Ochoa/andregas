using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Estoque.Historico;

namespace AndreGas.UnitTests.Features.Estoque;

public class ListarHistoricoEstoqueQueryHandlerTests
{
    [Fact]
    public async Task Handle_DeveRetornarHistoricoDoProdutoOrdenadoMaisRecentePrimeiro()
    {
        using var db = InMemoryDbContextFactory.Create();
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m);
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();

        var antigo = new HistoricoEstoque(produto.Id, TipoHistoricoEstoque.Entrada, "Entrada de 10 produtos", "Compra", "sistema", quantidade: 10, data: new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc));
        var recente = new HistoricoEstoque(produto.Id, TipoHistoricoEstoque.Venda, "Venda de 3 produtos", "Venda", "maria@teste.com", quantidade: 3, data: new DateTime(2026, 10, 7, 15, 0, 0, DateTimeKind.Utc));
        db.HistoricosEstoque.AddRange(antigo, recente);
        await db.SaveChangesAsync();

        var handler = new ListarHistoricoEstoqueQueryHandler(db);
        var result = await handler.Handle(new ListarHistoricoEstoqueQuery(produto.Id), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(TipoHistoricoEstoque.Venda, result[0].Tipo); // mais recente primeiro
        Assert.Equal(TipoHistoricoEstoque.Entrada, result[1].Tipo);
    }

    [Fact]
    public async Task Handle_DeveFiltrarApenasPeloProdutoInformado()
    {
        using var db = InMemoryDbContextFactory.Create();
        var produto1 = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m);
        var produto2 = new Produto("Água 20L", TipoProduto.Agua, 20m, 10m, 15m);
        db.Produtos.AddRange(produto1, produto2);
        await db.SaveChangesAsync();

        db.HistoricosEstoque.AddRange(
            new HistoricoEstoque(produto1.Id, TipoHistoricoEstoque.Cadastro, "Cadastro inicial", "Cadastro inicial", "sistema"),
            new HistoricoEstoque(produto2.Id, TipoHistoricoEstoque.Cadastro, "Cadastro inicial", "Cadastro inicial", "sistema"));
        await db.SaveChangesAsync();

        var handler = new ListarHistoricoEstoqueQueryHandler(db);
        var result = await handler.Handle(new ListarHistoricoEstoqueQuery(produto1.Id), CancellationToken.None);

        var historico = Assert.Single(result);
        Assert.Equal(produto1.Id, historico.ProdutoId);
    }

    [Fact]
    public async Task Handle_DeveRetornarListaVazia_QuandoProdutoSemHistorico()
    {
        using var db = InMemoryDbContextFactory.Create();
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m);
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();

        var handler = new ListarHistoricoEstoqueQueryHandler(db);
        var result = await handler.Handle(new ListarHistoricoEstoqueQuery(produto.Id), CancellationToken.None);

        Assert.Empty(result);
    }
}