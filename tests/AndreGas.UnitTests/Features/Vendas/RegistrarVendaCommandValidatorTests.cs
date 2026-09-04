using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Vendas.NovaVenda;

namespace AndreGas.UnitTests.Features.Vendas;

public class RegistrarVendaCommandValidatorTests
{
    private static async Task<AndreGas.Infrastructure.AppDbContext> CriarDbComProdutoAsync(int estoque = 10)
    {
        var db = InMemoryDbContextFactory.Create();
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m);
        produto.RegistrarEntrada(estoque);
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Validate_DeveSerValido_QuandoClienteExistenteEItensValidos()
    {
        using var db = await CriarDbComProdutoAsync();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var produtoId = db.Produtos.First().Id;
        var validator = new RegistrarVendaCommandValidator(db);

        var command = new RegistrarVendaCommand("11988887777", null, null, FormaPagamento.Dinheiro, 0m,
            [new ItemVendaInput(produtoId, 2)]);

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveExigirNomeEEndereco_QuandoClienteNaoExiste()
    {
        using var db = await CriarDbComProdutoAsync();
        var produtoId = db.Produtos.First().Id;
        var validator = new RegistrarVendaCommandValidator(db);

        var command = new RegistrarVendaCommand("11999998888", null, null, FormaPagamento.Dinheiro, 0m,
            [new ItemVendaInput(produtoId, 1)]);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegistrarVendaCommand.NomeClienteNovo));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegistrarVendaCommand.EnderecoClienteNovo));
    }

    [Fact]
    public async Task Validate_DeveSerValido_QuandoClienteNaoExisteMasNomeEEnderecoInformados()
    {
        using var db = await CriarDbComProdutoAsync();
        var produtoId = db.Produtos.First().Id;
        var validator = new RegistrarVendaCommandValidator(db);

        var command = new RegistrarVendaCommand("11999998888", "Novo Cliente", "Rua Nova, 10", FormaPagamento.Dinheiro, 0m,
            [new ItemVendaInput(produtoId, 1)]);

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoSemItens()
    {
        using var db = await CriarDbComProdutoAsync();
        var validator = new RegistrarVendaCommandValidator(db);

        var command = new RegistrarVendaCommand("11988887777", "X", "Y", FormaPagamento.Dinheiro, 0m, []);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoQuantidadeNaoPositiva()
    {
        using var db = await CriarDbComProdutoAsync();
        var produtoId = db.Produtos.First().Id;
        var validator = new RegistrarVendaCommandValidator(db);

        var command = new RegistrarVendaCommand("11988887777", "X", "Y", FormaPagamento.Dinheiro, 0m,
            [new ItemVendaInput(produtoId, 0)]);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoDescontoNegativo()
    {
        using var db = await CriarDbComProdutoAsync();
        var produtoId = db.Produtos.First().Id;
        var validator = new RegistrarVendaCommandValidator(db);

        var command = new RegistrarVendaCommand("11988887777", "X", "Y", FormaPagamento.Dinheiro, -10m,
            [new ItemVendaInput(produtoId, 1)]);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoEstoqueInsuficiente()
    {
        using var db = await CriarDbComProdutoAsync(estoque: 2);
        var produtoId = db.Produtos.First().Id;
        var validator = new RegistrarVendaCommandValidator(db);

        var command = new RegistrarVendaCommand("11988887777", "X", "Y", FormaPagamento.Dinheiro, 0m,
            [new ItemVendaInput(produtoId, 5)]);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoProdutoNaoExiste()
    {
        using var db = await CriarDbComProdutoAsync();
        var validator = new RegistrarVendaCommandValidator(db);

        var command = new RegistrarVendaCommand("11988887777", "X", "Y", FormaPagamento.Dinheiro, 0m,
            [new ItemVendaInput(Guid.NewGuid(), 1)]);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }
}
