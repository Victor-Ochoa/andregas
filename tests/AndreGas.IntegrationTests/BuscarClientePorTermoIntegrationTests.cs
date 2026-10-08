using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Web.Features.Vendas.NovaVenda;

namespace AndreGas.IntegrationTests.Tests;

/// <summary>
/// Testes da busca de clientes por termo (nome, telefone ou endereço) contra o Postgres real,
/// validando a tradução de <c>EF.Functions.ILike</c> do Npgsql (case-insensitive). Rodam em
/// integração porque o provider InMemory do EF não traduz ILike.
/// </summary>
public class BuscarClientePorTermoIntegrationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> CriarClienteAsync(string nome, string telefone, string endereco)
    {
        using var db = fixture.CreateDbContext();
        var cliente = new Cliente(nome, telefone, endereco);
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();
        return cliente.Id;
    }

    [Fact]
    public async Task Buscar_DeveEncontrarPorNome_CaseInsensitive()
    {
        var id = await CriarClienteAsync("Maria Souza", "11988887777", "Rua A, 1");

        using var db = fixture.CreateDbContext();
        var handler = new BuscarClientePorTermoQueryHandler(db);
        var result = await handler.Handle(new BuscarClientePorTermoQuery("maria"), CancellationToken.None);

        // Pode existir mais de uma "Maria" no banco compartilhado; o importante é que a
        // criada por este teste esteja nos resultados (case-insensitive).
        Assert.Contains(result, s => s.Id == id);
    }

    [Fact]
    public async Task Buscar_DeveEncontrarPorEndereco()
    {
        await CriarClienteAsync("Maria Souza", "11988887777", "Rua A, 1");

        using var db = fixture.CreateDbContext();
        var handler = new BuscarClientePorTermoQueryHandler(db);
        var result = await handler.Handle(new BuscarClientePorTermoQuery("rua a"), CancellationToken.None);

        Assert.Single(result);
    }

    [Fact]
    public async Task Buscar_DeveEncontrarPorTelefone()
    {
        await CriarClienteAsync("Maria Souza", "11988887777", "Rua A, 1");

        using var db = fixture.CreateDbContext();
        var handler = new BuscarClientePorTermoQueryHandler(db);
        var result = await handler.Handle(new BuscarClientePorTermoQuery("88887"), CancellationToken.None);

        Assert.Single(result);
    }

    [Fact]
    public async Task Buscar_DeveRetornarVazio_QuandoNaoExiste()
    {
        await CriarClienteAsync("Maria Souza", "11988887777", "Rua A, 1");

        using var db = fixture.CreateDbContext();
        var handler = new BuscarClientePorTermoQueryHandler(db);
        var result = await handler.Handle(new BuscarClientePorTermoQuery("zingale"), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Buscar_DeveRetornarTrazerSaldoDevedor_EUltimaValorEntrega()
    {
        var id = await CriarClienteAsync("Maria Souza", "11988887777", "Rua A, 1");

        using (var db = fixture.CreateDbContext())
        {
            var cliente = db.Clientes.First(c => c.Id == id);
            cliente.AdicionarSaldoDevedor(50m);
            var venda = new Venda(cliente.Id, FormaPagamento.Dinheiro);
            venda.DefinirValorEntrega(15m);
            db.Vendas.Add(venda);
            await db.SaveChangesAsync();
        }

        using var db2 = fixture.CreateDbContext();
        var handler = new BuscarClientePorTermoQueryHandler(db2);
        var result = await handler.Handle(new BuscarClientePorTermoQuery("maria"), CancellationToken.None);

        Assert.Contains(result, s => s.Id == id && s.SaldoDevedor == 50m && s.UltimaValorEntrega == 15m);
    }

    [Fact]
    public async Task Buscar_DeveLimitarAResultados()
    {
        // Cria 12 clientes que casam por "alpha".
        using (var db = fixture.CreateDbContext())
        {
            for (var i = 0; i < 12; i++)
            {
                db.Clientes.Add(new Cliente($"Cliente {i} Alpha", $"11998888{i:000}", $"Rua {i}, {i}"));
            }
            await db.SaveChangesAsync();
        }

        using var db2 = fixture.CreateDbContext();
        var handler = new BuscarClientePorTermoQueryHandler(db2);
        var result = await handler.Handle(new BuscarClientePorTermoQuery("alpha"), CancellationToken.None);

        Assert.Equal(10, result.Count);
    }
}