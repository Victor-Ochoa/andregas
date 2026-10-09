using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Admin.Vendas;
using AndreGas.Web.Features.Vendas.NovaVenda;

namespace AndreGas.UnitTests.Features.Admin.Vendas;

public class EditarVendaCommandValidatorTests
{
    private static async Task<AndreGas.Infrastructure.AppDbContext> CriarContextoAsync()
    {
        var db = InMemoryDbContextFactory.Create();

        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);

        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m);
        produto.RegistrarEntrada(10);
        db.Produtos.Add(produto);

        var venda = new Venda(cliente.Id, FormaPagamento.Dinheiro);
        venda.AdicionarItem(produto.Id, 1, 100m, 60m);
        db.Vendas.Add(venda);

        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Validate_DeveSerValido_QuandoComandoCorreto()
    {
        using var db = await CriarContextoAsync();
        var venda = db.Vendas.Single();
        var produto = db.Produtos.Single();
        var validator = new EditarVendaCommandValidator(db);

        var result = await validator.ValidateAsync(new EditarVendaCommand(
            venda.Id, FormaPagamento.Dinheiro, 0m, [new ItemVendaInput(produto.Id, 1)]));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoSemItens()
    {
        using var db = await CriarContextoAsync();
        var venda = db.Vendas.Single();
        var validator = new EditarVendaCommandValidator(db);

        var result = await validator.ValidateAsync(new EditarVendaCommand(
            venda.Id, FormaPagamento.Dinheiro, 0m, []));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("produto"));
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoDescontoNegativo()
    {
        using var db = await CriarContextoAsync();
        var venda = db.Vendas.Single();
        var produto = db.Produtos.Single();
        var validator = new EditarVendaCommandValidator(db);

        var result = await validator.ValidateAsync(new EditarVendaCommand(
            venda.Id, FormaPagamento.Dinheiro, -1m, [new ItemVendaInput(produto.Id, 1)]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(EditarVendaCommand.Desconto));
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoVendaNaoExiste()
    {
        using var db = await CriarContextoAsync();
        var produto = db.Produtos.Single();
        var validator = new EditarVendaCommandValidator(db);

        var result = await validator.ValidateAsync(new EditarVendaCommand(
            Guid.NewGuid(), FormaPagamento.Dinheiro, 0m, [new ItemVendaInput(produto.Id, 1)]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("não foi encontrada"));
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoQuantidadeZero()
    {
        using var db = await CriarContextoAsync();
        var venda = db.Vendas.Single();
        var produto = db.Produtos.Single();
        var validator = new EditarVendaCommandValidator(db);

        var result = await validator.ValidateAsync(new EditarVendaCommand(
            venda.Id, FormaPagamento.Dinheiro, 0m, [new ItemVendaInput(produto.Id, 0)]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("maior que zero"));
    }
}

public class ExcluirVendaCommandValidatorTests
{
    [Fact]
    public async Task Validate_DeveSerValido_QuandoVendaExiste()
    {
        using var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);

        var venda = new Venda(cliente.Id, FormaPagamento.Dinheiro);
        venda.AdicionarItem(Guid.NewGuid(), 1, 100m, 60m);
        db.Vendas.Add(venda);
        await db.SaveChangesAsync();

        var validator = new ExcluirVendaCommandValidator(db);
        var result = await validator.ValidateAsync(new ExcluirVendaCommand(venda.Id));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoVendaNaoExiste()
    {
        using var db = InMemoryDbContextFactory.Create();
        var validator = new ExcluirVendaCommandValidator(db);

        var result = await validator.ValidateAsync(new ExcluirVendaCommand(Guid.NewGuid()));

        Assert.False(result.IsValid);
    }
}