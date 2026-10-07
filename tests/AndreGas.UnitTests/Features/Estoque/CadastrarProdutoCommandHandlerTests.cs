using AndreGas.Domain.Enums;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Estoque.Cadastrar;

namespace AndreGas.UnitTests.Features.Estoque;

public class CadastrarProdutoCommandHandlerTests
{
    [Fact]
    public async Task Handle_DeveCriarProdutoComEstoqueZerado()
    {
        using var db = InMemoryDbContextFactory.Create();
        var handler = new CadastrarProdutoCommandHandler(db);

        var id = await handler.Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);

        var produto = await db.Produtos.FindAsync(id);
        Assert.NotNull(produto);
        Assert.Equal("Botijão 13kg", produto!.Nome);
        Assert.Equal(0, produto.QuantidadeEstoque);
        Assert.Equal(100m, produto.PrecoVenda);
        Assert.Equal(80m, produto.PrecoGasDoPovo);
    }

    [Fact]
    public async Task Handle_DeveRegistrarHistoricoDeCadastroInicial()
    {
        using var db = InMemoryDbContextFactory.Create();
        var handler = new CadastrarProdutoCommandHandler(db);

        var id = await handler.Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);

        var historico = Assert.Single(db.HistoricosEstoque);
        Assert.Equal(id, historico.ProdutoId);
        Assert.Equal(TipoHistoricoEstoque.Cadastro, historico.Tipo);
        Assert.Equal("Cadastro inicial", historico.Descricao);
        Assert.Equal("Cadastro inicial", historico.Motivo);
        Assert.Equal("sistema", historico.Usuario);
    }

    [Fact]
    public async Task Handle_ComUsuarioAutenticado_DeveRegistrarNomeNoHistorico()
    {
        using var db = InMemoryDbContextFactory.Create();
        var authProvider = new FakeAuthenticationStateProvider("maria@teste.com");
        var handler = new CadastrarProdutoCommandHandler(db, authProvider);

        var id = await handler.Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);

        var historico = Assert.Single(db.HistoricosEstoque);
        Assert.Equal("maria@teste.com", historico.Usuario);
    }
}
