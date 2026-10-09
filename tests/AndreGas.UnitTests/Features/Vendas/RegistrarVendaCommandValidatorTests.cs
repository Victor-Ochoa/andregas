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

        var command = new RegistrarVendaCommand(cliente.Id, null, null, null, FormaPagamento.Dinheiro, 0m,
            [new ItemVendaInput(produtoId, 2)]);

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveExigirCamposDoNovoCliente_QuandoClienteIdNaoInformado()
    {
        using var db = await CriarDbComProdutoAsync();
        var produtoId = db.Produtos.First().Id;
        var validator = new RegistrarVendaCommandValidator(db);

        var command = new RegistrarVendaCommand(null, null, null, null, FormaPagamento.Dinheiro, 0m,
            [new ItemVendaInput(produtoId, 1)]);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveSerValido_QuandoClienteNovoComTodosCampos()
    {
        using var db = await CriarDbComProdutoAsync();
        var produtoId = db.Produtos.First().Id;
        var validator = new RegistrarVendaCommandValidator(db);

        var command = new RegistrarVendaCommand(null, "Novo Cliente", "11999998888", "Rua Nova, 10",
            FormaPagamento.Dinheiro, 0m, [new ItemVendaInput(produtoId, 1)]);

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveExigirNome_QuandoClienteNovo()
    {
        using var db = await CriarDbComProdutoAsync();
        var produtoId = db.Produtos.First().Id;
        var validator = new RegistrarVendaCommandValidator(db);

        // Sem nome (mas com telefone/endereço).
        var command = new RegistrarVendaCommand(null, null, "11999998888", "Rua Nova, 10",
            FormaPagamento.Dinheiro, 0m, [new ItemVendaInput(produtoId, 1)]);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegistrarVendaCommand.NomeClienteNovo));
    }

    [Fact]
    public async Task Validate_DeveExigirTelefone_QuandoClienteNovo()
    {
        using var db = await CriarDbComProdutoAsync();
        var produtoId = db.Produtos.First().Id;
        var validator = new RegistrarVendaCommandValidator(db);

        // Sem telefone (mas com nome/endereço).
        var command = new RegistrarVendaCommand(null, "Novo Cliente", null, "Rua Nova, 10",
            FormaPagamento.Dinheiro, 0m, [new ItemVendaInput(produtoId, 1)]);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegistrarVendaCommand.TelefoneClienteNovo));
    }

    [Fact]
    public async Task Validate_DeveExigirEndereco_QuandoClienteNovo()
    {
        using var db = await CriarDbComProdutoAsync();
        var produtoId = db.Produtos.First().Id;
        var validator = new RegistrarVendaCommandValidator(db);

        // Sem endereço (mas com nome/telefone).
        var command = new RegistrarVendaCommand(null, "Novo Cliente", "11999998888", null,
            FormaPagamento.Dinheiro, 0m, [new ItemVendaInput(produtoId, 1)]);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegistrarVendaCommand.EnderecoClienteNovo));
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoClienteIdNaoExiste()
    {
        using var db = await CriarDbComProdutoAsync();
        var produtoId = db.Produtos.First().Id;
        var validator = new RegistrarVendaCommandValidator(db);

        var command = new RegistrarVendaCommand(Guid.NewGuid(), null, null, null, FormaPagamento.Dinheiro, 0m,
            [new ItemVendaInput(produtoId, 1)]);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoSemItens()
    {
        using var db = await CriarDbComProdutoAsync();
        var validator = new RegistrarVendaCommandValidator(db);

        var command = new RegistrarVendaCommand(null, "X", "11988887777", "Y", FormaPagamento.Dinheiro, 0m, []);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoQuantidadeNaoPositiva()
    {
        using var db = await CriarDbComProdutoAsync();
        var produtoId = db.Produtos.First().Id;
        var validator = new RegistrarVendaCommandValidator(db);

        var command = new RegistrarVendaCommand(null, "X", "11988887777", "Y", FormaPagamento.Dinheiro, 0m,
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

        var command = new RegistrarVendaCommand(null, "X", "11988887777", "Y", FormaPagamento.Dinheiro, -10m,
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

        var command = new RegistrarVendaCommand(null, "X", "11988887777", "Y", FormaPagamento.Dinheiro, 0m,
            [new ItemVendaInput(produtoId, 5)]);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoProdutoNaoExiste()
    {
        using var db = await CriarDbComProdutoAsync();
        var validator = new RegistrarVendaCommandValidator(db);

        var command = new RegistrarVendaCommand(null, "X", "11988887777", "Y", FormaPagamento.Dinheiro, 0m,
            [new ItemVendaInput(Guid.NewGuid(), 1)]);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoItemUsaProdutoDesabilitado()
    {
        using var db = await CriarDbComProdutoAsync();
        // Produto com estoque suficiente, mas desabilitado (não pode ser vendido).
        var produto = db.Produtos.First();
        produto.Desativar();
        await db.SaveChangesAsync();

        var validator = new RegistrarVendaCommandValidator(db);

        var command = new RegistrarVendaCommand(null, "X", "11988887777", "Y", FormaPagamento.Dinheiro, 0m,
            [new ItemVendaInput(produto.Id, 1)]);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoValorEntregaNegativo()
    {
        using var db = await CriarDbComProdutoAsync();
        var produtoId = db.Produtos.First().Id;
        var validator = new RegistrarVendaCommandValidator(db);

        var command = new RegistrarVendaCommand(null, "X", "11988887777", "Y", FormaPagamento.Dinheiro, 0m,
            [new ItemVendaInput(produtoId, 1)], ValorEntrega: -5m);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegistrarVendaCommand.ValorEntrega));
    }

    [Fact]
    public async Task Validate_DeveSerValido_QuandoValorEntregaZeroOuPositivo()
    {
        using var db = await CriarDbComProdutoAsync();
        var produtoId = db.Produtos.First().Id;
        var validator = new RegistrarVendaCommandValidator(db);

        var command = new RegistrarVendaCommand(null, "X", "11988887777", "Y", FormaPagamento.Dinheiro, 0m,
            [new ItemVendaInput(produtoId, 1)], ValorEntrega: 10m);

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    // --- Preço personalizado por cliente ---

    [Fact]
    public async Task Validate_DeveSerValido_QuandoPrecoUnitarioOmitido()
    {
        using var db = await CriarDbComProdutoAsync();
        var produtoId = db.Produtos.First().Id;
        var validator = new RegistrarVendaCommandValidator(db);

        var command = new RegistrarVendaCommand(null, "X", "11988887777", "Y", FormaPagamento.Dinheiro, 0m,
            [new ItemVendaInput(produtoId, 1)]);

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveSerValido_QuandoPrecoUnitarioPositivo()
    {
        using var db = await CriarDbComProdutoAsync();
        var produtoId = db.Produtos.First().Id;
        var validator = new RegistrarVendaCommandValidator(db);

        var command = new RegistrarVendaCommand(null, "X", "11988887777", "Y", FormaPagamento.Dinheiro, 0m,
            [new ItemVendaInput(produtoId, 1, PrecoUnitario: 50m)]);

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoPrecoUnitarioZeroOuNegativo()
    {
        using var db = await CriarDbComProdutoAsync();
        var produtoId = db.Produtos.First().Id;
        var validator = new RegistrarVendaCommandValidator(db);

        var command = new RegistrarVendaCommand(null, "X", "11988887777", "Y", FormaPagamento.Dinheiro, 0m,
            [new ItemVendaInput(produtoId, 1, PrecoUnitario: 0m)]);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Itens[0].PrecoUnitario");
    }
}