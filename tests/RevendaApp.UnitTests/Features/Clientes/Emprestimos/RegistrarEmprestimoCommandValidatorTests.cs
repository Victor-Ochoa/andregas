using RevendaApp.Domain.Entities;
using RevendaApp.Domain.Enums;
using RevendaApp.Infrastructure;
using RevendaApp.UnitTests.TestHelpers;
using RevendaApp.Web.Features.Clientes.Emprestimos;

namespace RevendaApp.UnitTests.Features.Clientes.Emprestimos;

public class RegistrarEmprestimoCommandValidatorTests
{
    private static async Task<AppDbContext> CriarAsync(int vasilhamesDisponiveis = 10)
    {
        var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, controlaVasilhame: true);
        if (vasilhamesDisponiveis > 0)
        {
            produto.RegistrarEntradaVasilhame(vasilhamesDisponiveis);
        }

        db.Clientes.Add(cliente);
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Validate_DeveSerValido_QuandoProdutoControlaVasilhame_ClienteExiste_EQuantidadeValida()
    {
        using var db = await CriarAsync();
        var validator = new RegistrarEmprestimoCommandValidator(db);
        var cliente = db.Clientes.First();
        var produto = db.Produtos.First();

        var result = await validator.ValidateAsync(new RegistrarEmprestimoCommand(cliente.Id, produto.Id, 3));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoVasilhamesDisponiveisInsuficientes()
    {
        using var db = await CriarAsync(vasilhamesDisponiveis: 2);
        var validator = new RegistrarEmprestimoCommandValidator(db);
        var cliente = db.Clientes.First();
        var produto = db.Produtos.First();

        var result = await validator.ValidateAsync(new RegistrarEmprestimoCommand(cliente.Id, produto.Id, 5));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegistrarEmprestimoCommand.Quantidade));
        Assert.Contains("Vasilhames disponíveis insuficientes", result.Errors.First(e => e.PropertyName == nameof(RegistrarEmprestimoCommand.Quantidade)).ErrorMessage);
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoClienteNaoExiste()
    {
        using var db = await CriarAsync();
        var validator = new RegistrarEmprestimoCommandValidator(db);
        var produto = db.Produtos.First();

        var result = await validator.ValidateAsync(new RegistrarEmprestimoCommand(Guid.NewGuid(), produto.Id, 3));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_DeveSerInvalido_QuandoProdutoNaoControlaVasilhame()
    {
        using var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        var produto = new Produto("Água 20L", TipoProduto.Agua, 20m, 10m, 15m, controlaVasilhame: false);
        db.Clientes.Add(cliente);
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();
        var validator = new RegistrarEmprestimoCommandValidator(db);

        var result = await validator.ValidateAsync(new RegistrarEmprestimoCommand(cliente.Id, produto.Id, 3));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegistrarEmprestimoCommand.ProdutoId));
    }
}