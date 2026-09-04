using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Estoque.Listar;

namespace AndreGas.UnitTests.Features.Estoque;

public class ListarProdutosQueryHandlerTests
{
    [Fact]
    public async Task Handle_DeveRetornarProdutosOrdenadosPorNomeComIndicadorDeEstoqueBaixo()
    {
        using var db = InMemoryDbContextFactory.Create();

        var produtoBaixo = new Produto("Zeta Água 20L", TipoProduto.Agua, 10m, 6m, 8m, estoqueMinimo: 5);
        produtoBaixo.RegistrarEntrada(3); // abaixo do mínimo

        var produtoOk = new Produto("Alfa Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, estoqueMinimo: 5);
        produtoOk.RegistrarEntrada(20);

        db.Produtos.AddRange(produtoBaixo, produtoOk);
        await db.SaveChangesAsync();

        var handler = new ListarProdutosQueryHandler(db);
        var result = await handler.Handle(new ListarProdutosQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("Alfa Botijão 13kg", result[0].Nome);
        Assert.False(result[0].EstoqueBaixo);
        Assert.Equal("Zeta Água 20L", result[1].Nome);
        Assert.True(result[1].EstoqueBaixo);
    }
}
