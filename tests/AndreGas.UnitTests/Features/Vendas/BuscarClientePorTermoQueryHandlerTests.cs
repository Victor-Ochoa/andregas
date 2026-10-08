using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Vendas.NovaVenda;

namespace AndreGas.UnitTests.Features.Vendas;

/// <summary>
/// Testes da busca de clientes pelo termo (nome, telefone ou endereço) usada no autocomplete
/// da tela de nova venda.
/// </summary>
public class BuscarClientePorTermoQueryHandlerTests
{
    [Fact]
    public async Task Handle_DeveRetornarClientes_QuandoTermoCasarComNome()
    {
        var db = InMemoryDbContextFactory.Create();
        db.Clientes.Add(new Cliente("Maria Souza", "11988887777", "Rua A, 1"));
        db.Clientes.Add(new Cliente("João Silva", "11999998888", "Rua B, 2"));
        await db.SaveChangesAsync();

        var handler = new BuscarClientePorTermoQueryHandler(db);
        var result = await handler.Handle(new BuscarClientePorTermoQuery("maria"), CancellationToken.None);

        var cliente = Assert.Single(result);
        Assert.Equal("Maria Souza", cliente.Nome);
    }

    [Fact]
    public async Task Handle_DeveRetornarClientes_QuandoTermoCasarComTelefone()
    {
        var db = InMemoryDbContextFactory.Create();
        db.Clientes.Add(new Cliente("Maria Souza", "11988887777", "Rua A, 1"));
        db.Clientes.Add(new Cliente("João Silva", "11999998888", "Rua B, 2"));
        await db.SaveChangesAsync();

        var handler = new BuscarClientePorTermoQueryHandler(db);
        var result = await handler.Handle(new BuscarClientePorTermoQuery("88887"), CancellationToken.None);

        var cliente = Assert.Single(result);
        Assert.Equal("Maria Souza", cliente.Nome);
    }

    [Fact]
    public async Task Handle_DeveRetornarClientes_QuandoTermoCasarComEndereco()
    {
        var db = InMemoryDbContextFactory.Create();
        db.Clientes.Add(new Cliente("Maria Souza", "11988887777", "Rua A, 1"));
        db.Clientes.Add(new Cliente("João Silva", "11999998888", "Rua B, 2"));
        await db.SaveChangesAsync();

        var handler = new BuscarClientePorTermoQueryHandler(db);
        var result = await handler.Handle(new BuscarClientePorTermoQuery("rua a"), CancellationToken.None);

        var cliente = Assert.Single(result);
        Assert.Equal("Maria Souza", cliente.Nome);
    }

    [Fact]
    public async Task Handle_DeveSerCaseInsensitive_NoNome()
    {
        var db = InMemoryDbContextFactory.Create();
        db.Clientes.Add(new Cliente("Maria Souza", "11988887777", "Rua A, 1"));
        await db.SaveChangesAsync();

        var handler = new BuscarClientePorTermoQueryHandler(db);
        var result = await handler.Handle(new BuscarClientePorTermoQuery("MARIA"), CancellationToken.None);

        Assert.Single(result);
    }

    [Fact]
    public async Task Handle_DeveOrdenarPorNome_QuandoVariosResultados()
    {
        var db = InMemoryDbContextFactory.Create();
        db.Clientes.Add(new Cliente("Zelma", "11999990000", "Rua Z, 9"));
        db.Clientes.Add(new Cliente("Ana", "11988881111", "Rua A, 1"));
        db.Clientes.Add(new Cliente("Bruno", "11977772222", "Rua B, 2"));
        await db.SaveChangesAsync();

        var handler = new BuscarClientePorTermoQueryHandler(db);
        var result = await handler.Handle(new BuscarClientePorTermoQuery("a"), CancellationToken.None);

        Assert.Equal(["Ana", "Bruno", "Zelma"], result.Select(r => r.Nome).ToArray());
    }

    [Fact]
    public async Task Handle_DeveLimitarResultados_AosPrimeirosDez()
    {
        var db = InMemoryDbContextFactory.Create();
        // 15 clientes com "a" no nome (todos ainda com telefone único).
        for (var i = 0; i < 15; i++)
        {
            db.Clientes.Add(new Cliente($"Cliente {i} Alpha", $"11998888{i:000}", $"Rua {i}, {i}"));
        }
        await db.SaveChangesAsync();

        var handler = new BuscarClientePorTermoQueryHandler(db);
        var result = await handler.Handle(new BuscarClientePorTermoQuery("alpha"), CancellationToken.None);

        Assert.Equal(10, result.Count);
    }

    [Fact]
    public async Task Handle_DeveRetornarVazio_QuandoTermoNaoCasarComNada()
    {
        var db = InMemoryDbContextFactory.Create();
        db.Clientes.Add(new Cliente("Maria Souza", "11988887777", "Rua A, 1"));
        await db.SaveChangesAsync();

        var handler = new BuscarClientePorTermoQueryHandler(db);
        var result = await handler.Handle(new BuscarClientePorTermoQuery("xyz"), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_DeveRetornarVazio_QuandoTermoVazioOuBranco()
    {
        var db = InMemoryDbContextFactory.Create();
        db.Clientes.Add(new Cliente("Maria Souza", "11988887777", "Rua A, 1"));
        await db.SaveChangesAsync();

        var handler = new BuscarClientePorTermoQueryHandler(db);

        Assert.Empty(await handler.Handle(new BuscarClientePorTermoQuery(""), CancellationToken.None));
        Assert.Empty(await handler.Handle(new BuscarClientePorTermoQuery("   "), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_DeveTrazerSaldoDevedor_EUltimoValorDeEntrega()
    {
        var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        cliente.AdicionarSaldoDevedor(50m);
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var venda = new Venda(cliente.Id, FormaPagamento.Dinheiro);
        venda.DefinirValorEntrega(15m);
        db.Vendas.Add(venda);
        await db.SaveChangesAsync();

        var handler = new BuscarClientePorTermoQueryHandler(db);
        var result = await handler.Handle(new BuscarClientePorTermoQuery("maria"), CancellationToken.None);

        var sugestao = Assert.Single(result);
        Assert.Equal(cliente.Id, sugestao.Id);
        Assert.Equal(50m, sugestao.SaldoDevedor);
        Assert.Equal(15m, sugestao.UltimaValorEntrega);
    }
}