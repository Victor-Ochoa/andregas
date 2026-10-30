using RevendaApp.Domain.Entities;
using RevendaApp.Domain.Enums;
using RevendaApp.UnitTests.TestHelpers;
using RevendaApp.Web.Features.Clientes.Listar;

namespace RevendaApp.UnitTests.Features.Clientes;

public class ListarClientesQueryHandlerTests
{
    [Fact]
    public async Task Handle_DeveRetornarClientesOrdenadosPorNome()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        db.Clientes.AddRange(
            new Cliente("Zeca Alves", "11911111111", "Rua Z, 1"),
            new Cliente("Ana Paula", "11922222222", "Rua A, 2"));
        await db.SaveChangesAsync();

        var handler = new ListarClientesQueryHandler(db, TenantTeste.Contexto());

        var result = await handler.Handle(new ListarClientesQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("Ana Paula", result[0].Nome);
        Assert.Equal("Zeca Alves", result[1].Nome);
    }

    [Fact]
    public async Task Handle_DeveRetornarListaVazia_QuandoNaoHaClientes()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var handler = new ListarClientesQueryHandler(db, TenantTeste.Contexto());

        var result = await handler.Handle(new ListarClientesQuery(), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_DeveExporTotalDeVasilhamesEmprestadosPorCliente()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        var outroCliente = new Cliente("João Pereira", "11977776666", "Rua B, 2");
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, controlaVasilhame: true);
        db.Clientes.AddRange(cliente, outroCliente);
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();

        // Maria: 2 empréstimos abertos (10 + 5 de saldo em aberto) e 1 quitado (saldo 0).
        db.EmprestimosVasilhames.Add(new EmprestimoVasilhame(cliente.Id, produto.Id, 10, new DateTime(2026, 1, 5, 10, 0, 0, DateTimeKind.Utc)));
        db.EmprestimosVasilhames.Add(new EmprestimoVasilhame(cliente.Id, produto.Id, 5, new DateTime(2026, 1, 8, 10, 0, 0, DateTimeKind.Utc)));
        var quitado = new EmprestimoVasilhame(cliente.Id, produto.Id, 4, new DateTime(2026, 1, 9, 10, 0, 0, DateTimeKind.Utc));
        quitado.RegistrarDevolucao(4); // quitado → fora do saldo em aberto
        db.EmprestimosVasilhames.Add(quitado);

        // João: 1 empréstimo aberto, mas deve ser filtrado por cliente.
        db.EmprestimosVasilhames.Add(new EmprestimoVasilhame(outroCliente.Id, produto.Id, 8, new DateTime(2026, 1, 10, 10, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var handler = new ListarClientesQueryHandler(db, TenantTeste.Contexto());
        var result = await handler.Handle(new ListarClientesQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(15, result.Single(c => c.Id == cliente.Id).QuantidadeVasilhamesEmprestados); // 10 + 5
        Assert.Equal(8, result.Single(c => c.Id == outroCliente.Id).QuantidadeVasilhamesEmprestados);
    }

    [Fact]
    public async Task Handle_ClienteSemEmprestimos_DeveTerZeroVasilhamesEmprestados()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        db.Clientes.Add(new Cliente("Maria Souza", "11988887777", "Rua A, 1"));
        await db.SaveChangesAsync();

        var handler = new ListarClientesQueryHandler(db, TenantTeste.Contexto());
        var result = await handler.Handle(new ListarClientesQuery(), CancellationToken.None);

        var item = Assert.Single(result);
        Assert.Equal(0, item.QuantidadeVasilhamesEmprestados);
    }
}