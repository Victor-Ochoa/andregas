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

    [Fact]
    public async Task Handle_DeveRetornarApenasProdutosAtivos_PorPadrao()
    {
        using var db = InMemoryDbContextFactory.Create();

        var produtoAtivo = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m);
        var produtoInativo = new Produto("Galão Água 20L", TipoProduto.Agua, 20m, 10m, 15m);
        produtoInativo.Desativar();

        db.Produtos.AddRange(produtoAtivo, produtoInativo);
        await db.SaveChangesAsync();

        var handler = new ListarProdutosQueryHandler(db);
        var result = await handler.Handle(new ListarProdutosQuery(), CancellationToken.None);

        var produto = Assert.Single(result);
        Assert.Equal("Botijão 13kg", produto.Nome);
        Assert.True(produto.Ativo);
    }

    [Fact]
    public async Task Handle_ComIncluirInativos_DeveRetornarTodosComFlagAtivo()
    {
        using var db = InMemoryDbContextFactory.Create();

        var produtoAtivo = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m);
        var produtoInativo = new Produto("Galão Água 20L", TipoProduto.Agua, 20m, 10m, 15m);
        produtoInativo.Desativar();

        db.Produtos.AddRange(produtoAtivo, produtoInativo);
        await db.SaveChangesAsync();

        var handler = new ListarProdutosQueryHandler(db);
        var result = await handler.Handle(new ListarProdutosQuery(IncluirInativos: true), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.True(result.Single(p => p.Nome == "Botijão 13kg").Ativo);
        Assert.False(result.Single(p => p.Nome == "Galão Água 20L").Ativo);
    }

    [Fact]
    public async Task Handle_DeveExporContadoresDeVasilhame()
    {
        using var db = InMemoryDbContextFactory.Create();

        var produtoComVasilhames = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, controlaVasilhame: true);
        produtoComVasilhames.RegistrarEntrada(10);         // 10 cheios
        produtoComVasilhames.RegistrarEntradaVasilhame(5); // 5 vazios disponíveis
        produtoComVasilhames.RegistrarEmprestimoVasilhame(3); // 3 emprestados

        var produtoSemVasilhames = new Produto("Galão Água 20L", TipoProduto.Agua, 20m, 10m, 15m);
        produtoSemVasilhames.RegistrarEntrada(7);

        db.Produtos.AddRange(produtoComVasilhames, produtoSemVasilhames);
        await db.SaveChangesAsync();

        var handler = new ListarProdutosQueryHandler(db);
        var result = await handler.Handle(new ListarProdutosQuery(), CancellationToken.None);

        var comVasilhames = result.Single(p => p.Id == produtoComVasilhames.Id);
        Assert.True(comVasilhames.ControlaVasilhame);
        Assert.Equal(10, comVasilhames.QuantidadeEstoque);
        Assert.Equal(5, comVasilhames.QuantidadeVasilhames);
        Assert.Equal(3, comVasilhames.QuantidadeVasilhamesEmprestados);
        Assert.Equal(8, comVasilhames.TotalVasilhames); // 5 disponíveis + 3 emprestados
        Assert.Equal(15, comVasilhames.Patio);          // 10 cheios + 5 vazios

        var semVasilhames = result.Single(p => p.Id == produtoSemVasilhames.Id);
        Assert.False(semVasilhames.ControlaVasilhame);
        Assert.Equal(7, semVasilhames.QuantidadeEstoque);
        Assert.Equal(0, semVasilhames.QuantidadeVasilhames);
        Assert.Equal(0, semVasilhames.QuantidadeVasilhamesEmprestados);
        Assert.Equal(0, semVasilhames.TotalVasilhames);
        Assert.Equal(7, semVasilhames.Patio); // sem controle = só o estoque cheio
    }
}