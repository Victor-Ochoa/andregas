using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Estoque.Vasilhames;

namespace AndreGas.UnitTests.Features.Estoque.Vasilhames;

public class ObterClientesComEmprestimoProdutoQueryHandlerTests
{
    private static async Task<(AppDbContext Db, Cliente Maria, Cliente Joao, Produto Produto)> CriarAsync()
    {
        var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var maria = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        var joao = new Cliente("João Pereira", "11977776666", "Rua B, 2");
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, controlaVasilhame: true);
        db.Clientes.AddRange(maria, joao);
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();
        return (db, maria, joao, produto);
    }

    private static async Task RegistrarEmprestimoAsync(
        AppDbContext db, Cliente cliente, Produto produto, int quantidade, int quantidadeDevolvida = 0)
    {
        var emprestimo = new EmprestimoVasilhame(cliente.Id, produto.Id, quantidade, new DateTime(2026, 1, 5, 10, 0, 0, DateTimeKind.Utc), null);
        if (quantidadeDevolvida > 0)
        {
            emprestimo.RegistrarDevolucao(quantidadeDevolvida);
        }

        db.EmprestimosVasilhames.Add(emprestimo);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_DeveRetornarApenasClientesComSaldoEmAbertoDoProduto()
    {
        var (db, maria, joao, produto) = await CriarAsync();
        await RegistrarEmprestimoAsync(db, maria, produto, 3);
        await RegistrarEmprestimoAsync(db, joao, produto, 5, quantidadeDevolvida: 5); // quitado
        var handler = new ObterClientesComEmprestimoProdutoQueryHandler(db, TenantTeste.Contexto());

        var result = await handler.Handle(new ObterClientesComEmprestimoProdutoQuery(produto.Id), CancellationToken.None);

        var item = Assert.Single(result);
        Assert.Equal(maria.Id, item.ClienteId);
        Assert.Equal("Maria Souza", item.ClienteNome);
        Assert.Equal("11988887777", item.Telefone);
        Assert.Equal(3, item.QuantidadeEmprestada);
    }

    [Fact]
    public async Task Handle_DeveAgruparEmprestimosDoMesmoCliente()
    {
        var (db, maria, _, produto) = await CriarAsync();
        await RegistrarEmprestimoAsync(db, maria, produto, 3);
        await RegistrarEmprestimoAsync(db, maria, produto, 2);
        var handler = new ObterClientesComEmprestimoProdutoQueryHandler(db, TenantTeste.Contexto());

        var result = await handler.Handle(new ObterClientesComEmprestimoProdutoQuery(produto.Id), CancellationToken.None);

        var item = Assert.Single(result);
        Assert.Equal(maria.Id, item.ClienteId);
        Assert.Equal(5, item.QuantidadeEmprestada); // 3 + 2
    }

    [Fact]
    public async Task Handle_DeveDesconsiderarEmprestimoDeOutroProduto()
    {
        var (db, maria, _, produto) = await CriarAsync();
        var outro = new Produto("Galão 20L", TipoProduto.Agua, 20m, 10m, 15m, controlaVasilhame: true);
        db.Produtos.Add(outro);
        await db.SaveChangesAsync();
        await RegistrarEmprestimoAsync(db, maria, outro, 4);
        var handler = new ObterClientesComEmprestimoProdutoQueryHandler(db, TenantTeste.Contexto());

        var result = await handler.Handle(new ObterClientesComEmprestimoProdutoQuery(produto.Id), CancellationToken.None);

        Assert.Empty(result);
    }
}