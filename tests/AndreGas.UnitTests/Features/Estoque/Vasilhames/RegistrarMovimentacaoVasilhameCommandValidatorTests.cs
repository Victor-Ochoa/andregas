using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Estoque.Vasilhames;

namespace AndreGas.UnitTests.Features.Estoque.Vasilhames;

public class RegistrarMovimentacaoVasilhameCommandValidatorTests
{
    private static RegistrarMovimentacaoVasilhameCommandValidator CriarValidator(
        AndreGas.Infrastructure.AppDbContext db) => new(db);

    private static async Task<AndreGas.Infrastructure.AppDbContext> CriarAsync(bool controlaVasilhame = true)
    {
        var db = InMemoryDbContextFactory.Create();
        db.Produtos.Add(new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, controlaVasilhame: controlaVasilhame));
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Validate_DevePassar_QuandoComandoValido()
    {
        using var db = await CriarAsync();
        var validator = CriarValidator(db);

        var result = await validator.ValidateAsync(new RegistrarMovimentacaoVasilhameCommand(
            db.Produtos.First().Id, TipoMovimentacaoEstoque.Entrada, 5, "Observação"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveFalhar_QuandoProdutoNaoControlaVasilhame()
    {
        using var db = await CriarAsync(controlaVasilhame: false);
        var validator = CriarValidator(db);

        var result = await validator.ValidateAsync(new RegistrarMovimentacaoVasilhameCommand(
            db.Produtos.First().Id, TipoMovimentacaoEstoque.Entrada, 5));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegistrarMovimentacaoVasilhameCommand.ProdutoId));
    }

    [Fact]
    public async Task Validate_DeveFalhar_QuandoProdutoNaoExiste()
    {
        using var db = await CriarAsync();
        var validator = CriarValidator(db);

        var result = await validator.ValidateAsync(new RegistrarMovimentacaoVasilhameCommand(
            Guid.NewGuid(), TipoMovimentacaoEstoque.Entrada, 5));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegistrarMovimentacaoVasilhameCommand.ProdutoId));
    }

    [Fact]
    public async Task Validate_DeveFalhar_QuandoQuantidadeMenorOuIgualAZero_EmEntradaOuSaida()
    {
        using var db = await CriarAsync();
        var validator = CriarValidator(db);

        var entrada = await validator.ValidateAsync(new RegistrarMovimentacaoVasilhameCommand(
            db.Produtos.First().Id, TipoMovimentacaoEstoque.Entrada, 0));
        var saida = await validator.ValidateAsync(new RegistrarMovimentacaoVasilhameCommand(
            db.Produtos.First().Id, TipoMovimentacaoEstoque.Saida, 0));

        Assert.False(entrada.IsValid);
        Assert.False(saida.IsValid);
        Assert.Contains(entrada.Errors, e => e.PropertyName == nameof(RegistrarMovimentacaoVasilhameCommand.Quantidade));
    }

    [Fact]
    public async Task Validate_DevePermitirQuantidadeZero_NoAjuste()
    {
        using var db = await CriarAsync();
        var validator = CriarValidator(db);

        var result = await validator.ValidateAsync(new RegistrarMovimentacaoVasilhameCommand(
            db.Produtos.First().Id, TipoMovimentacaoEstoque.Ajuste, 0));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveFalhar_QuandoQuantidadeNegativa_NoAjuste()
    {
        using var db = await CriarAsync();
        var validator = CriarValidator(db);

        var result = await validator.ValidateAsync(new RegistrarMovimentacaoVasilhameCommand(
            db.Produtos.First().Id, TipoMovimentacaoEstoque.Ajuste, -1));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegistrarMovimentacaoVasilhameCommand.Quantidade));
    }

    [Fact]
    public async Task Validate_DeveFalhar_QuandoTipoInvalido()
    {
        using var db = await CriarAsync();
        var validator = CriarValidator(db);

        var result = await validator.ValidateAsync(new RegistrarMovimentacaoVasilhameCommand(
            db.Produtos.First().Id, (TipoMovimentacaoEstoque)999, 5));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegistrarMovimentacaoVasilhameCommand.Tipo));
    }

    [Fact]
    public async Task Validate_DeveFalhar_QuandoObservacaoMuitoLonga()
    {
        using var db = await CriarAsync();
        var validator = CriarValidator(db);

        var result = await validator.ValidateAsync(new RegistrarMovimentacaoVasilhameCommand(
            db.Produtos.First().Id, TipoMovimentacaoEstoque.Entrada, 5, new string('x', 301)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegistrarMovimentacaoVasilhameCommand.Observacao));
    }
}