using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Common;
using AndreGas.Web.Features.Clientes.Emprestimos;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.UnitTests.Features.Clientes.Emprestimos;

public class ObterEmprestimosClienteQueryHandlerTests
{
    private static async Task<(AppDbContext Db, Cliente Cliente, Produto ProdutoA, Produto ProdutoB)> CriarAsync()
    {
        var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        var produtoA = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, controlaVasilhame: true);
        var produtoB = new Produto("Botijão 05kg", TipoProduto.Gas, 50m, 30m, 40m, controlaVasilhame: true);
        db.Clientes.Add(cliente);
        db.Produtos.AddRange(produtoA, produtoB);
        await db.SaveChangesAsync();
        return (db, cliente, produtoA, produtoB);
    }

    private static async Task RegistrarEmprestimoAsync(
        AppDbContext db,
        Guid clienteId,
        Guid produtoId,
        int quantidade,
        DateTime data,
        int quantidadeDevolvida = 0)
    {
        var produto = await db.Produtos.FindAsync(produtoId);
        produto!.RegistrarEmprestimoVasilhame(quantidade);
        var emprestimo = new EmprestimoVasilhame(clienteId, produtoId, quantidade, data, null);
        if (quantidadeDevolvida > 0)
        {
            emprestimo.RegistrarDevolucao(quantidadeDevolvida);
        }

        db.EmprestimosVasilhames.Add(emprestimo);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_DeveRetornarEmprestimosAbertos_AgrupadosPorProduto_ComSaldoEDataMaisAntiga()
    {
        var (db, cliente, produtoA, produtoB) = await CriarAsync();
        await RegistrarEmprestimoAsync(db, cliente.Id, produtoA.Id, 3, new DateTime(2026, 1, 5, 10, 0, 0, DateTimeKind.Utc));
        await RegistrarEmprestimoAsync(db, cliente.Id, produtoA.Id, 2, new DateTime(2026, 1, 10, 10, 0, 0, DateTimeKind.Utc));
        await RegistrarEmprestimoAsync(db, cliente.Id, produtoB.Id, 4, new DateTime(2026, 1, 8, 10, 0, 0, DateTimeKind.Utc));
        var handler = new ObterEmprestimosClienteQueryHandler(db, TenantTeste.Contexto());

        var result = await handler.Handle(new ObterEmprestimosClienteQuery(cliente.Id), CancellationToken.None);

        Assert.Equal(2, result.Count);
        var itemA = Assert.Single(result, i => i.ProdutoId == produtoA.Id);
        Assert.Equal("Botijão 13kg", itemA.ProdutoNome);
        Assert.Equal(5, itemA.Quantidade); // 3 + 2 saldo em aberto
        Assert.Equal(
            BrasilTimeZone.ParaBrasilia(new DateTime(2026, 1, 5, 10, 0, 0, DateTimeKind.Utc)),
            itemA.Data); // empréstimo mais antigo do produto

        var itemB = Assert.Single(result, i => i.ProdutoId == produtoB.Id);
        Assert.Equal("Botijão 05kg", itemB.ProdutoNome);
        Assert.Equal(4, itemB.Quantidade);
        Assert.Equal(
            BrasilTimeZone.ParaBrasilia(new DateTime(2026, 1, 8, 10, 0, 0, DateTimeKind.Utc)),
            itemB.Data);
    }

    [Fact]
    public async Task Handle_DevolucaoParcial_DeveReduzirQuantidadeEmAberto_DoItem()
    {
        var (db, cliente, produtoA, _) = await CriarAsync();
        await RegistrarEmprestimoAsync(
            db,
            cliente.Id,
            produtoA.Id,
            10,
            new DateTime(2026, 1, 5, 10, 0, 0, DateTimeKind.Utc),
            quantidadeDevolvida: 3);
        var handler = new ObterEmprestimosClienteQueryHandler(db, TenantTeste.Contexto());

        var result = await handler.Handle(new ObterEmprestimosClienteQuery(cliente.Id), CancellationToken.None);

        var item = Assert.Single(result);
        Assert.Equal(produtoA.Id, item.ProdutoId);
        Assert.Equal(7, item.Quantidade); // 10 emprestados − 3 devolvidos
    }

    [Fact]
    public async Task Handle_EmprestimosQuitados_DeveFicarForaDaLista()
    {
        var (db, cliente, produtoA, _) = await CriarAsync();
        await RegistrarEmprestimoAsync(
            db,
            cliente.Id,
            produtoA.Id,
            6,
            new DateTime(2026, 1, 5, 10, 0, 0, DateTimeKind.Utc),
            quantidadeDevolvida: 6); // quitado → DevolvidoEm definido
        var handler = new ObterEmprestimosClienteQueryHandler(db, TenantTeste.Contexto());

        var result = await handler.Handle(new ObterEmprestimosClienteQuery(cliente.Id), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_ClienteSemEmprestimos_DeveRetornarListaVazia()
    {
        var (db, cliente, _, _) = await CriarAsync();
        var handler = new ObterEmprestimosClienteQueryHandler(db, TenantTeste.Contexto());

        var result = await handler.Handle(new ObterEmprestimosClienteQuery(cliente.Id), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_DeveFiltrarEmprestimosDeOutroCliente()
    {
        var (db, cliente, produtoA, _) = await CriarAsync();
        var outroCliente = new Cliente("João Pereira", "11977776666", "Rua B, 2");
        db.Clientes.Add(outroCliente);
        await db.SaveChangesAsync();

        await RegistrarEmprestimoAsync(db, outroCliente.Id, produtoA.Id, 9, new DateTime(2026, 1, 5, 10, 0, 0, DateTimeKind.Utc));
        var handler = new ObterEmprestimosClienteQueryHandler(db, TenantTeste.Contexto());

        var result = await handler.Handle(new ObterEmprestimosClienteQuery(cliente.Id), CancellationToken.None);

        Assert.Empty(result);
    }
}