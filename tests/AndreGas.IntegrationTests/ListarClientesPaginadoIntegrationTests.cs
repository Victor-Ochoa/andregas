using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Web.Features.Clientes.Listar;
using AndreGas.IntegrationTests.TestHelpers;

namespace AndreGas.IntegrationTests.Tests;

/// <summary>
/// Testes de integração da listagem paginada de clientes contra o Postgres real, validando a
/// tradução de <c>EF.Functions.ILike</c> do Npgsql (case-insensitive) e a combinação de busca e
/// filtros. Rodam em integração porque o provider InMemory do EF não traduz ILike.
/// </summary>
public class ListarClientesPaginadoIntegrationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
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
        var handler = new ListarClientesPaginadoQueryHandler(db, TenantIntegracao.Contexto());
        var result = await handler.Handle(
            new ListarClientesPaginadoQuery(Termo: "MARIA", Ativo: null, ComSaldoDevedor: null, ComEmprestimo: null, Pagina: 1, TamanhoPagina: 20),
            CancellationToken.None);

        Assert.Equal(1, result.Total);
        var item = Assert.Single(result.Itens);
        Assert.Equal(id, item.Id);
    }

    [Fact]
    public async Task Buscar_DeveEncontrarPorEndereco_SemDistinguirMaiusculas()
    {
        await CriarClienteAsync("Maria Souza", "11988887777", "Rua A, 1");

        using var db = fixture.CreateDbContext();
        var handler = new ListarClientesPaginadoQueryHandler(db, TenantIntegracao.Contexto());
        var result = await handler.Handle(
            new ListarClientesPaginadoQuery(Termo: "rua a", Ativo: null, ComSaldoDevedor: null, ComEmprestimo: null, Pagina: 1, TamanhoPagina: 20),
            CancellationToken.None);

        Assert.Single(result.Itens);
    }

    [Fact]
    public async Task Buscar_DeveEncontrarPorTelefone()
    {
        await CriarClienteAsync("Maria Souza", "11988887777", "Rua A, 1");

        using var db = fixture.CreateDbContext();
        var handler = new ListarClientesPaginadoQueryHandler(db, TenantIntegracao.Contexto());
        var result = await handler.Handle(
            new ListarClientesPaginadoQuery(Termo: "88887", Ativo: null, ComSaldoDevedor: null, ComEmprestimo: null, Pagina: 1, TamanhoPagina: 20),
            CancellationToken.None);

        Assert.Single(result.Itens);
    }

    [Fact]
    public async Task Buscar_DeveTratarCuringaComoLiteral()
    {
        await CriarClienteAsync("Maria 100% Souza", "11988887777", "Rua A, 1");

        using var db = fixture.CreateDbContext();
        var handler = new ListarClientesPaginadoQueryHandler(db, TenantIntegracao.Contexto());

        // O "%" digitado pelo usuário não deve se comportar como curinga de LIKE: buscar "100%"
        // casa só quem tem literalmente "100%" no nome (não qualquer nome com "100").
        var comCuringa = await handler.Handle(
            new ListarClientesPaginadoQuery(Termo: "100%", Ativo: null, ComSaldoDevedor: null, ComEmprestimo: null, Pagina: 1, TamanhoPagina: 20),
            CancellationToken.None);
        Assert.Single(comCuringa.Itens);

        var semCorrespondencia = await handler.Handle(
            new ListarClientesPaginadoQuery(Termo: "100_", Ativo: null, ComSaldoDevedor: null, ComEmprestimo: null, Pagina: 1, TamanhoPagina: 20),
            CancellationToken.None);
        Assert.Empty(semCorrespondencia.Itens);
    }

    [Fact]
    public async Task Buscar_DeveRetornarTotalEItensDaPaginaCorreta()
    {
        using (var db = fixture.CreateDbContext())
        {
            for (var i = 0; i < 6; i++)
            {
                db.Clientes.Add(new Cliente($"Cliente Alpha {i}", $"11998888{i:000}", $"Rua {i}, {i}"));
            }
            await db.SaveChangesAsync();
        }

        using var db2 = fixture.CreateDbContext();
        var handler = new ListarClientesPaginadoQueryHandler(db2, TenantIntegracao.Contexto());

        var pagina1 = await handler.Handle(
            new ListarClientesPaginadoQuery(Termo: "alpha", Ativo: null, ComSaldoDevedor: null, ComEmprestimo: null, Pagina: 1, TamanhoPagina: 4),
            CancellationToken.None);
        Assert.Equal(6, pagina1.Total);
        Assert.Equal(2, pagina1.TotalPaginas);
        Assert.Equal(4, pagina1.Itens.Count);

        var pagina2 = await handler.Handle(
            new ListarClientesPaginadoQuery(Termo: "alpha", Ativo: null, ComSaldoDevedor: null, ComEmprestimo: null, Pagina: 2, TamanhoPagina: 4),
            CancellationToken.None);
        Assert.Equal(2, pagina2.Itens.Count);
    }

    [Fact]
    public async Task BuscarComFiltroComSaldoDevedor_DeveRespeitarAmbos()
    {
        using (var db = fixture.CreateDbContext())
        {
            var semSaldo = new Cliente("Joana Alpha", "11911111111", "Rua A, 1");
            var comSaldo = new Cliente("Maria Alpha", "11922222222", "Rua B, 2");
            comSaldo.AdicionarSaldoDevedor(80m);
            db.Clientes.AddRange(semSaldo, comSaldo);
            await db.SaveChangesAsync();
        }

        using var db2 = fixture.CreateDbContext();
        var handler = new ListarClientesPaginadoQueryHandler(db2, TenantIntegracao.Contexto());

        var result = await handler.Handle(
            new ListarClientesPaginadoQuery(Termo: "alpha", Ativo: null, ComSaldoDevedor: true, ComEmprestimo: null, Pagina: 1, TamanhoPagina: 20),
            CancellationToken.None);

        var item = Assert.Single(result.Itens);
        Assert.Equal("Maria Alpha", item.Nome);
    }

    [Fact]
    public async Task FiltroComEmprestimo_DeveRetornarQuemTemVasilhameEmAberto_IgnorandoTermoVazio()
    {
        using (var db = fixture.CreateDbContext())
        {
            var maria = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
            var joao = new Cliente("João Pereira", "11977776666", "Rua B, 2");
            var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, controlaVasilhame: true);
            db.Clientes.AddRange(maria, joao);
            db.Produtos.Add(produto);
            await db.SaveChangesAsync();

            db.EmprestimosVasilhames.Add(new EmprestimoVasilhame(maria.Id, produto.Id, 10, new DateTime(2026, 1, 5, 10, 0, 0, DateTimeKind.Utc)));
            await db.SaveChangesAsync();
        }

        using var db2 = fixture.CreateDbContext();
        var handler = new ListarClientesPaginadoQueryHandler(db2, TenantIntegracao.Contexto());

        // Termo vazio + ComEmprestimo: retorna apenas Maria (João não tem empréstimo aberto).
        var result = await handler.Handle(
            new ListarClientesPaginadoQuery(Termo: "", Ativo: null, ComSaldoDevedor: null, ComEmprestimo: true, Pagina: 1, TamanhoPagina: 20),
            CancellationToken.None);

        var item = Assert.Single(result.Itens);
        Assert.Equal("Maria Souza", item.Nome);
        Assert.Equal(10, item.QuantidadeVasilhamesEmprestados);
    }
}