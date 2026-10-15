using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Estoque.Vasilhames;

namespace AndreGas.UnitTests.Features.Estoque;

public class RegistrarEntradaVasilhameCommandValidatorTests
{
    private static async Task<AndreGas.Infrastructure.AppDbContext> CriarProdutoAsync(bool controlaVasilhame = true)
    {
        var db = InMemoryDbContextFactory.Create();
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, controlaVasilhame: controlaVasilhame);
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Validate_DeveSerValido_QuandoProdutoControlaVasilhameEQuantidadePositiva()
    {
        using var db = await CriarProdutoAsync();
        var validator = new RegistrarEntradaVasilhameCommandValidator(db);

        var result = await validator.ValidateAsync(new RegistrarEntradaVasilhameCommand(db.Produtos.First().Id, 10, "Teste"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoProdutoNaoControlaVasilhame()
    {
        using var db = await CriarProdutoAsync(controlaVasilhame: false);
        var validator = new RegistrarEntradaVasilhameCommandValidator(db);

        var result = await validator.ValidateAsync(new RegistrarEntradaVasilhameCommand(db.Produtos.First().Id, 10));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoProdutoNaoExiste()
    {
        using var db = await CriarProdutoAsync();
        var validator = new RegistrarEntradaVasilhameCommandValidator(db);

        var result = await validator.ValidateAsync(new RegistrarEntradaVasilhameCommand(Guid.NewGuid(), 10));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoQuantidadeNaoPositiva()
    {
        using var db = await CriarProdutoAsync();
        var validator = new RegistrarEntradaVasilhameCommandValidator(db);

        var result = await validator.ValidateAsync(new RegistrarEntradaVasilhameCommand(db.Produtos.First().Id, 0));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegistrarEntradaVasilhameCommand.Quantidade));
    }
}