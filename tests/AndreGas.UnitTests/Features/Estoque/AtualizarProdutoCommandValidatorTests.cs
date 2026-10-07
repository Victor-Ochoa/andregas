using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Estoque.Editar;

namespace AndreGas.UnitTests.Features.Estoque;

public class AtualizarProdutoCommandValidatorTests
{
    private static async Task<AndreGas.Infrastructure.AppDbContext> CriarDbComProdutoAsync()
    {
        var db = InMemoryDbContextFactory.Create();
        db.Produtos.Add(new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m));
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Validate_DeveSerValido_QuandoDadosCorretos()
    {
        using var db = await CriarDbComProdutoAsync();
        var produtoId = db.Produtos.First().Id;
        var validator = new AtualizarProdutoCommandValidator(db);

        var command = new AtualizarProdutoCommand(produtoId, "Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5, Ativo: true);

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoNomeVazio()
    {
        using var db = await CriarDbComProdutoAsync();
        var produtoId = db.Produtos.First().Id;
        var validator = new AtualizarProdutoCommandValidator(db);

        var command = new AtualizarProdutoCommand(produtoId, "", TipoProduto.Gas, 100m, 60m, 80m, 5, Ativo: true);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AtualizarProdutoCommand.Nome));
    }

    [Theory]
    [InlineData(-1, 60, 80)]
    [InlineData(100, -1, 80)]
    [InlineData(100, 60, -1)]
    public async Task Validate_DeveSerInvalido_QuandoAlgumPrecoNegativo(decimal venda, decimal custo, decimal gasDoPovo)
    {
        using var db = await CriarDbComProdutoAsync();
        var produtoId = db.Produtos.First().Id;
        var validator = new AtualizarProdutoCommandValidator(db);

        var command = new AtualizarProdutoCommand(produtoId, "Botijão 13kg", TipoProduto.Gas, venda, custo, gasDoPovo, 5, Ativo: true);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoEstoqueMinimoNegativo()
    {
        using var db = await CriarDbComProdutoAsync();
        var produtoId = db.Produtos.First().Id;
        var validator = new AtualizarProdutoCommandValidator(db);

        var command = new AtualizarProdutoCommand(produtoId, "Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, -1, Ativo: true);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoProdutoNaoExiste()
    {
        using var db = await CriarDbComProdutoAsync();
        var validator = new AtualizarProdutoCommandValidator(db);

        var command = new AtualizarProdutoCommand(Guid.NewGuid(), "X", TipoProduto.Gas, 100m, 60m, 80m, 5, Ativo: true);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }
}