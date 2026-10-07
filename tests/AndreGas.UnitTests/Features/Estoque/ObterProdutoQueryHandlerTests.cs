using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Estoque.Editar;

namespace AndreGas.UnitTests.Features.Estoque;

public class ObterProdutoQueryHandlerTests
{
    [Fact]
    public async Task Handle_DeveRetornarDadosDoProduto()
    {
        using var db = InMemoryDbContextFactory.Create();
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, estoqueMinimo: 5);
        produto.Desativar();
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();

        var handler = new ObterProdutoQueryHandler(db);
        var result = await handler.Handle(new ObterProdutoQuery(produto.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(produto.Id, result!.Id);
        Assert.Equal("Botijão 13kg", result.Nome);
        Assert.Equal(TipoProduto.Gas, result.Tipo);
        Assert.Equal(100m, result.PrecoVenda);
        Assert.Equal(60m, result.PrecoCusto);
        Assert.Equal(80m, result.PrecoGasDoPovo);
        Assert.Equal(5, result.EstoqueMinimo);
        Assert.False(result.Ativo);
    }

    [Fact]
    public async Task Handle_DeveRetornarNull_QuandoProdutoNaoExiste()
    {
        using var db = InMemoryDbContextFactory.Create();
        var handler = new ObterProdutoQueryHandler(db);

        var result = await handler.Handle(new ObterProdutoQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }
}