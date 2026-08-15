using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Estoque.Movimentar;

namespace AndreGas.UnitTests.Features.Estoque;

public class RegistrarMovimentacaoEstoqueCommandValidatorTests
{
    [Fact]
    public async Task Validate_DeveSerValido_QuandoProdutoExisteEQuantidadePositiva()
    {
        using var db = InMemoryDbContextFactory.Create();
        var produto = new Produto("Botijão 13kg", TipoProduto.GasBotijao13, 100m, 60m, 80m);
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();

        var validator = new RegistrarMovimentacaoEstoqueCommandValidator(db);
        var result = await validator.ValidateAsync(new RegistrarMovimentacaoEstoqueCommand(produto.Id, TipoMovimentacaoEstoque.Entrada, 10, null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoProdutoNaoExiste()
    {
        using var db = InMemoryDbContextFactory.Create();
        var validator = new RegistrarMovimentacaoEstoqueCommandValidator(db);

        var result = await validator.ValidateAsync(new RegistrarMovimentacaoEstoqueCommand(Guid.NewGuid(), TipoMovimentacaoEstoque.Entrada, 10, null));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoQuantidadeNaoPositiva()
    {
        using var db = InMemoryDbContextFactory.Create();
        var produto = new Produto("Botijão 13kg", TipoProduto.GasBotijao13, 100m, 60m, 80m);
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();

        var validator = new RegistrarMovimentacaoEstoqueCommandValidator(db);
        var result = await validator.ValidateAsync(new RegistrarMovimentacaoEstoqueCommand(produto.Id, TipoMovimentacaoEstoque.Entrada, 0, null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegistrarMovimentacaoEstoqueCommand.Quantidade));
    }
}
