using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Estoque.Movimentar;

namespace AndreGas.UnitTests.Features.Estoque;

public class RegistrarMovimentacaoEstoqueCommandHandlerTests
{
    private static async Task<(AndreGas.Infrastructure.AppDbContext Db, Produto Produto)> CriarProdutoAsync(int estoqueInicial = 0)
    {
        var db = InMemoryDbContextFactory.Create();
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m);
        if (estoqueInicial > 0)
        {
            produto.RegistrarEntrada(estoqueInicial);
        }

        db.Produtos.Add(produto);
        await db.SaveChangesAsync();
        return (db, produto);
    }

    [Fact]
    public async Task Handle_Entrada_DeveAumentarEstoqueECriarMovimentacao()
    {
        var (db, produto) = await CriarProdutoAsync();
        var handler = new RegistrarMovimentacaoEstoqueCommandHandler(db);

        var sucesso = await handler.Handle(new RegistrarMovimentacaoEstoqueCommand(produto.Id, TipoMovimentacaoEstoque.Entrada, 10, "Compra do fornecedor"), CancellationToken.None);

        Assert.True(sucesso);
        var atualizado = await db.Produtos.FindAsync(produto.Id);
        Assert.Equal(10, atualizado!.QuantidadeEstoque);

        var movimentacao = Assert.Single(db.MovimentacoesEstoque);
        Assert.Equal(TipoMovimentacaoEstoque.Entrada, movimentacao.Tipo);
        Assert.Equal(10, movimentacao.Quantidade);
        Assert.Equal("Compra do fornecedor", movimentacao.Motivo);
    }

    [Fact]
    public async Task Handle_Saida_DeveDiminuirEstoque()
    {
        var (db, produto) = await CriarProdutoAsync(estoqueInicial: 10);
        var handler = new RegistrarMovimentacaoEstoqueCommandHandler(db);

        var sucesso = await handler.Handle(new RegistrarMovimentacaoEstoqueCommand(produto.Id, TipoMovimentacaoEstoque.Saida, 4, null), CancellationToken.None);

        Assert.True(sucesso);
        var atualizado = await db.Produtos.FindAsync(produto.Id);
        Assert.Equal(6, atualizado!.QuantidadeEstoque);
    }

    [Fact]
    public async Task Handle_Saida_DeveLancarExcecao_QuandoEstoqueInsuficiente()
    {
        var (db, produto) = await CriarProdutoAsync(estoqueInicial: 2);
        var handler = new RegistrarMovimentacaoEstoqueCommandHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new RegistrarMovimentacaoEstoqueCommand(produto.Id, TipoMovimentacaoEstoque.Saida, 5, null), CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Handle_Ajuste_DeveDefinirQuantidadeExata()
    {
        var (db, produto) = await CriarProdutoAsync(estoqueInicial: 10);
        var handler = new RegistrarMovimentacaoEstoqueCommandHandler(db);

        var sucesso = await handler.Handle(new RegistrarMovimentacaoEstoqueCommand(produto.Id, TipoMovimentacaoEstoque.Ajuste, 3, "Inventário"), CancellationToken.None);

        Assert.True(sucesso);
        var atualizado = await db.Produtos.FindAsync(produto.Id);
        Assert.Equal(3, atualizado!.QuantidadeEstoque);
    }

    [Fact]
    public async Task Handle_DeveRetornarFalso_QuandoProdutoNaoExiste()
    {
        using var db = InMemoryDbContextFactory.Create();
        var handler = new RegistrarMovimentacaoEstoqueCommandHandler(db);

        var sucesso = await handler.Handle(new RegistrarMovimentacaoEstoqueCommand(Guid.NewGuid(), TipoMovimentacaoEstoque.Entrada, 5, null), CancellationToken.None);

        Assert.False(sucesso);
    }

    [Fact]
    public async Task Handle_Entrada_DeveRegistrarHistoricoComUsuario()
    {
        var (db, produto) = await CriarProdutoAsync();
        var authProvider = new FakeAuthenticationStateProvider("maria@teste.com");
        var handler = new RegistrarMovimentacaoEstoqueCommandHandler(db, authProvider);

        await handler.Handle(new RegistrarMovimentacaoEstoqueCommand(produto.Id, TipoMovimentacaoEstoque.Entrada, 10, "Compra do fornecedor"), CancellationToken.None);

        var historico = Assert.Single(db.HistoricosEstoque);
        Assert.Equal(TipoHistoricoEstoque.Entrada, historico.Tipo);
        Assert.Equal(10, historico.Quantidade);
        Assert.Equal("Compra do fornecedor", historico.Motivo);
        Assert.Equal("maria@teste.com", historico.Usuario);
        Assert.Equal("Entrada de 10 produtos", historico.Descricao);
    }

    [Fact]
    public async Task Handle_SemProvider_DeveRegistrarHistoricoComUsuarioSistema()
    {
        var (db, produto) = await CriarProdutoAsync();
        var handler = new RegistrarMovimentacaoEstoqueCommandHandler(db);

        await handler.Handle(new RegistrarMovimentacaoEstoqueCommand(produto.Id, TipoMovimentacaoEstoque.Entrada, 10, null), CancellationToken.None);

        var historico = Assert.Single(db.HistoricosEstoque);
        Assert.Equal("sistema", historico.Usuario);
        Assert.Equal("Entrada de 10 produtos", historico.Descricao);
    }

    [Fact]
    public async Task Handle_Ajuste_DeveRegistrarHistoricoComQuantidadeAlvo()
    {
        var (db, produto) = await CriarProdutoAsync(estoqueInicial: 10);
        var handler = new RegistrarMovimentacaoEstoqueCommandHandler(db);

        await handler.Handle(new RegistrarMovimentacaoEstoqueCommand(produto.Id, TipoMovimentacaoEstoque.Ajuste, 3, "Inventário"), CancellationToken.None);

        var historico = Assert.Single(db.HistoricosEstoque);
        Assert.Equal(TipoHistoricoEstoque.Ajuste, historico.Tipo);
        Assert.Equal(3, historico.Quantidade);
        Assert.Equal("Ajuste de estoque para 3", historico.Descricao);
    }

    [Fact]
    public async Task Handle_Saida_DeveRegistrarHistorico()
    {
        var (db, produto) = await CriarProdutoAsync(estoqueInicial: 10);
        var handler = new RegistrarMovimentacaoEstoqueCommandHandler(db);

        await handler.Handle(new RegistrarMovimentacaoEstoqueCommand(produto.Id, TipoMovimentacaoEstoque.Saida, 4, null), CancellationToken.None);

        var historico = Assert.Single(db.HistoricosEstoque);
        Assert.Equal(TipoHistoricoEstoque.Saida, historico.Tipo);
        Assert.Equal(4, historico.Quantidade);
        Assert.Equal("Saída de 4 produtos", historico.Descricao);
    }
}