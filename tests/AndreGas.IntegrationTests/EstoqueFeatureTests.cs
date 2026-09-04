using AndreGas.Domain.Enums;
using AndreGas.Web.Features.Estoque.Cadastrar;
using AndreGas.Web.Features.Estoque.Listar;
using AndreGas.Web.Features.Estoque.Movimentar;

namespace AndreGas.IntegrationTests.Tests;

/// <summary>
/// Testes de integração dos handlers de Estoque contra um Postgres real (via
/// <see cref="DatabaseFixture"/>), cobrindo cadastro, listagem e movimentações.
/// </summary>
public class EstoqueFeatureTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        // Reset completo: o Postgres do AppHost usa volume persistente, então outras classes de
        // teste podem ter deixado dados (ex.: Vendas referenciando Produtos).
        await fixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CadastrarEListar_DeveExibirProdutoComEstoqueZerado()
    {
        using var db = fixture.CreateDbContext();
        var cadastrarHandler = new CadastrarProdutoCommandHandler(db);
        var listarHandler = new ListarProdutosQueryHandler(db);

        await cadastrarHandler.Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);

        var produtos = await listarHandler.Handle(new ListarProdutosQuery(), CancellationToken.None);

        var produto = Assert.Single(produtos);
        Assert.Equal("Botijão 13kg", produto.Nome);
        Assert.Equal(0, produto.QuantidadeEstoque);
        Assert.True(produto.EstoqueBaixo); // 0 <= 5 (estoque mínimo)
    }

    [Fact]
    public async Task RegistrarEntradaESaida_DeveRefletirNaListagem()
    {
        using var db = fixture.CreateDbContext();
        var cadastrarHandler = new CadastrarProdutoCommandHandler(db);
        var movimentarHandler = new RegistrarMovimentacaoEstoqueCommandHandler(db);
        var listarHandler = new ListarProdutosQueryHandler(db);

        var produtoId = await cadastrarHandler.Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);

        await movimentarHandler.Handle(new RegistrarMovimentacaoEstoqueCommand(produtoId, TipoMovimentacaoEstoque.Entrada, 20, "Compra"), CancellationToken.None);
        await movimentarHandler.Handle(new RegistrarMovimentacaoEstoqueCommand(produtoId, TipoMovimentacaoEstoque.Saida, 7, "Venda avulsa"), CancellationToken.None);

        var produtos = await listarHandler.Handle(new ListarProdutosQuery(), CancellationToken.None);

        var produto = Assert.Single(produtos);
        Assert.Equal(13, produto.QuantidadeEstoque);
        Assert.False(produto.EstoqueBaixo);
    }
}
