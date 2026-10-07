using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Estoque.Editar;

namespace AndreGas.UnitTests.Features.Estoque;

public class AtualizarProdutoCommandHandlerTests
{
    private static async Task<AndreGas.Infrastructure.AppDbContext> CriarDbComProdutoAsync()
    {
        var db = InMemoryDbContextFactory.Create();
        db.Produtos.Add(new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, estoqueMinimo: 5));
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Handle_DeveAtualizarDadosEAtivo()
    {
        using var db = await CriarDbComProdutoAsync();
        var produto = db.Produtos.First();
        var handler = new AtualizarProdutoCommandHandler(db);

        var atualizado = await handler.Handle(new AtualizarProdutoCommand(
            produto.Id, "Botijão Novo", TipoProduto.Agua, 120m, 70m, 90m, 3, Ativo: true), CancellationToken.None);

        Assert.True(atualizado);
        var resultado = await db.Produtos.FindAsync(produto.Id);
        Assert.Equal("Botijão Novo", resultado!.Nome);
        Assert.Equal(TipoProduto.Agua, resultado.Tipo);
        Assert.Equal(120m, resultado.PrecoVenda);
        Assert.Equal(70m, resultado.PrecoCusto);
        Assert.Equal(90m, resultado.PrecoGasDoPovo);
        Assert.Equal(3, resultado.EstoqueMinimo);
        Assert.True(resultado.Ativo);
    }

    [Fact]
    public async Task Handle_DeveDesativar_QuandoAtivoFalse()
    {
        using var db = await CriarDbComProdutoAsync();
        var produto = db.Produtos.First();
        var handler = new AtualizarProdutoCommandHandler(db);

        await handler.Handle(new AtualizarProdutoCommand(
            produto.Id, "Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5, Ativo: false), CancellationToken.None);

        var resultado = await db.Produtos.FindAsync(produto.Id);
        Assert.False(resultado!.Ativo);
    }

    [Fact]
    public async Task Handle_DeveRetornarFalso_QuandoProdutoNaoExiste()
    {
        using var db = await CriarDbComProdutoAsync();
        var handler = new AtualizarProdutoCommandHandler(db);

        var atualizado = await handler.Handle(new AtualizarProdutoCommand(
            Guid.NewGuid(), "X", TipoProduto.Gas, 100m, 60m, 80m, 5, Ativo: true), CancellationToken.None);

        Assert.False(atualizado);
    }
}