using AndreGas.Domain.Enums;
using AndreGas.Web.Features.Estoque.Cadastrar;

namespace AndreGas.UnitTests.Features.Estoque;

public class CadastrarProdutoCommandValidatorTests
{
    private readonly CadastrarProdutoCommandValidator _validator = new();

    [Fact]
    public void Validate_DeveSerValido_QuandoDadosCorretos()
    {
        var result = _validator.Validate(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_DeveSerInvalido_QuandoNomeVazio()
    {
        var result = _validator.Validate(new CadastrarProdutoCommand("", TipoProduto.Gas, 100m, 60m, 80m, 5));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CadastrarProdutoCommand.Nome));
    }

    [Theory]
    [InlineData(-1, 60, 80)]
    [InlineData(100, -1, 80)]
    [InlineData(100, 60, -1)]
    public void Validate_DeveSerInvalido_QuandoAlgumPrecoNegativo(decimal precoVenda, decimal precoCusto, decimal precoGasDoPovo)
    {
        var result = _validator.Validate(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, precoVenda, precoCusto, precoGasDoPovo, 5));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_DeveSerInvalido_QuandoEstoqueMinimoNegativo()
    {
        var result = _validator.Validate(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, -1));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CadastrarProdutoCommand.EstoqueMinimo));
    }
}