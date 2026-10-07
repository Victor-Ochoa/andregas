using AndreGas.Domain.Enums;
using AndreGas.Web.Features.Estoque.Cadastrar;
using AndreGas.Web.Features.Estoque.Editar;
using AndreGas.Web.Features.Estoque.Historico;
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

    [Fact]
    public async Task EditarProduto_DeveAlterarDadosEPersistir()
    {
        using var db = fixture.CreateDbContext();
        var cadastrarHandler = new CadastrarProdutoCommandHandler(db);
        var editarHandler = new AtualizarProdutoCommandHandler(db);
        var listarHandler = new ListarProdutosQueryHandler(db);

        var produtoId = await cadastrarHandler.Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);

        var atualizado = await editarHandler.Handle(new AtualizarProdutoCommand(
            produtoId, "Botijão Novo", TipoProduto.Agua, 120m, 70m, 90m, 3, Ativo: true), CancellationToken.None);

        Assert.True(atualizado);

        var produtos = await listarHandler.Handle(new ListarProdutosQuery(), CancellationToken.None);
        var produto = Assert.Single(produtos);
        Assert.Equal("Botijão Novo", produto.Nome);
        Assert.Equal(TipoProduto.Agua, produto.Tipo);
        Assert.Equal(120m, produto.PrecoVenda);
        Assert.Equal(3, produto.EstoqueMinimo);
        Assert.True(produto.Ativo);
    }

    [Fact]
    public async Task DesativarProdutoViaEdicao_DeveSairDaListagemPadraoESomarIncluirInativos()
    {
        using var db = fixture.CreateDbContext();
        var cadastrarHandler = new CadastrarProdutoCommandHandler(db);
        var editarHandler = new AtualizarProdutoCommandHandler(db);
        var listarHandler = new ListarProdutosQueryHandler(db);

        var produtoId = await cadastrarHandler.Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);

        var atualizado = await editarHandler.Handle(new AtualizarProdutoCommand(
            produtoId, "Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5, Ativo: false), CancellationToken.None);
        Assert.True(atualizado);

        // Listagem padrão (nova venda): não inclui o inativo.
        var ativos = await listarHandler.Handle(new ListarProdutosQuery(), CancellationToken.None);
        Assert.Empty(ativos);

        // Tela de estoque (IncluirInativos): lista com flag Ativo false.
        var todos = await listarHandler.Handle(new ListarProdutosQuery(IncluirInativos: true), CancellationToken.None);
        var produto = Assert.Single(todos);
        Assert.False(produto.Ativo);
    }

    [Fact]
    public async Task ReativarProdutoViaEdicao_DeveVoltarAAparecerNaListagemPadrao()
    {
        using var db = fixture.CreateDbContext();
        var cadastrarHandler = new CadastrarProdutoCommandHandler(db);
        var editarHandler = new AtualizarProdutoCommandHandler(db);
        var listarHandler = new ListarProdutosQueryHandler(db);

        var produtoId = await cadastrarHandler.Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);

        await editarHandler.Handle(new AtualizarProdutoCommand(
            produtoId, "Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5, Ativo: false), CancellationToken.None);
        await editarHandler.Handle(new AtualizarProdutoCommand(
            produtoId, "Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5, Ativo: true), CancellationToken.None);

        var ativos = await listarHandler.Handle(new ListarProdutosQuery(), CancellationToken.None);
        var produto = Assert.Single(ativos);
        Assert.True(produto.Ativo);
    }

    [Fact]
    public async Task CadastroMovimentacaoEEdicao_DeveGerarHistoricoDeEstoque()
    {
        using var db = fixture.CreateDbContext();
        var cadastrarHandler = new CadastrarProdutoCommandHandler(db);
        var movimentarHandler = new RegistrarMovimentacaoEstoqueCommandHandler(db);
        var editarHandler = new AtualizarProdutoCommandHandler(db);
        var listarHistoricoHandler = new ListarHistoricoEstoqueQueryHandler(db);

        var produtoId = await cadastrarHandler.Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);
        await movimentarHandler.Handle(new RegistrarMovimentacaoEstoqueCommand(produtoId, TipoMovimentacaoEstoque.Entrada, 10, "Compra"), CancellationToken.None);
        await editarHandler.Handle(new AtualizarProdutoCommand(produtoId, "Botijão Novo", TipoProduto.Gas, 120m, 60m, 80m, 5, Ativo: true), CancellationToken.None);

        var historico = await listarHistoricoHandler.Handle(new ListarHistoricoEstoqueQuery(produtoId), CancellationToken.None);

        // Cadastro, Entrada e Edição (mais recente primeiro).
        Assert.Equal(3, historico.Count);
        Assert.Contains(historico, h => h.Tipo == TipoHistoricoEstoque.Cadastro && h.Usuario == "sistema");
        Assert.Contains(historico, h => h.Tipo == TipoHistoricoEstoque.Entrada && h.Quantidade == 10);
        Assert.Contains(historico, h => h.Tipo == TipoHistoricoEstoque.Edicao && h.Descricao!.Contains("Preço de venda"));
    }
}
