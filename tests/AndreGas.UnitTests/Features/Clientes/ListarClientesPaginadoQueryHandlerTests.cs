using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Clientes.Listar;

namespace AndreGas.UnitTests.Features.Clientes;

public class ListarClientesPaginadoQueryHandlerTests
{
    [Fact]
    public async Task Handle_DeveRetornarPaginaOrdenadaPorNome_ComTotalETotalPaginasCorretos()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        db.Clientes.AddRange(
            new Cliente("Zeca Alves", "11911111111", "Rua Z, 1"),
            new Cliente("Ana Paula", "11922222222", "Rua A, 2"),
            new Cliente("Bruno Lima", "11933333333", "Rua B, 3"),
            new Cliente("Carla Dias", "11944444444", "Rua C, 4"),
            new Cliente("Diego Rios", "11955555555", "Rua D, 5"));
        await db.SaveChangesAsync();

        var handler = new ListarClientesPaginadoQueryHandler(db, TenantTeste.Contexto());

        // Página 1 de 2 (TamanhoPagina=2): Ana Paula e Bruno Lima (ordem alfabética).
        var pagina1 = await handler.Handle(
            new ListarClientesPaginadoQuery(Termo: null, Ativo: null, ComSaldoDevedor: null, ComEmprestimo: null, Pagina: 1, TamanhoPagina: 2),
            CancellationToken.None);

        Assert.Equal(5, pagina1.Total);
        Assert.Equal(3, pagina1.TotalPaginas);
        Assert.Equal(2, pagina1.Itens.Count);
        Assert.Equal("Ana Paula", pagina1.Itens[0].Nome);
        Assert.Equal("Bruno Lima", pagina1.Itens[1].Nome);

        // Última página: resta apenas Zeca Alves.
        var pagina3 = await handler.Handle(
            new ListarClientesPaginadoQuery(Termo: null, Ativo: null, ComSaldoDevedor: null, ComEmprestimo: null, Pagina: 3, TamanhoPagina: 2),
            CancellationToken.None);

        Assert.Single(pagina3.Itens);
        Assert.Equal("Zeca Alves", pagina3.Itens[0].Nome);
    }

    [Fact]
    public async Task Handle_DeveRetornarVazio_QuandoPaginaForaDoLimite()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        db.Clientes.Add(new Cliente("Ana Paula", "11922222222", "Rua A, 2"));
        await db.SaveChangesAsync();

        var handler = new ListarClientesPaginadoQueryHandler(db, TenantTeste.Contexto());

        var result = await handler.Handle(
            new ListarClientesPaginadoQuery(Termo: null, Ativo: null, ComSaldoDevedor: null, ComEmprestimo: null, Pagina: 99, TamanhoPagina: 20),
            CancellationToken.None);

        Assert.Equal(1, result.Total);
        Assert.Empty(result.Itens);
    }

    [Fact]
    public async Task Handle_DeveRetornarTotalZero_QuandoNaoHaClientes()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var handler = new ListarClientesPaginadoQueryHandler(db, TenantTeste.Contexto());

        var result = await handler.Handle(
            new ListarClientesPaginadoQuery(Termo: null, Ativo: null, ComSaldoDevedor: null, ComEmprestimo: null, Pagina: 1, TamanhoPagina: 20),
            CancellationToken.None);

        Assert.Empty(result.Itens);
        Assert.Equal(0, result.Total);
        Assert.Equal(0, result.TotalPaginas);
    }

    [Fact]
    public async Task Handle_FiltroAtivo_DeveRetornarApenasClientesAtivos()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var ativo = new Cliente("Ana Paula", "11922222222", "Rua A, 2");
        var inativo = new Cliente("Bruno Lima", "11933333333", "Rua B, 3");
        inativo.Desativar();
        db.Clientes.AddRange(ativo, inativo);
        await db.SaveChangesAsync();

        var handler = new ListarClientesPaginadoQueryHandler(db, TenantTeste.Contexto());

        var result = await handler.Handle(
            new ListarClientesPaginadoQuery(Termo: null, Ativo: true, ComSaldoDevedor: null, ComEmprestimo: null, Pagina: 1, TamanhoPagina: 20),
            CancellationToken.None);

        var item = Assert.Single(result.Itens);
        Assert.Equal(ativo.Id, item.Id);
    }

    [Fact]
    public async Task Handle_FiltroComSaldoDevedor_DeveRetornarApenasQuemTemSaldo()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var comSaldo = new Cliente("Ana Paula", "11922222222", "Rua A, 2");
        comSaldo.AdicionarSaldoDevedor(50m);
        var semSaldo = new Cliente("Bruno Lima", "11933333333", "Rua B, 3");
        db.Clientes.AddRange(comSaldo, semSaldo);
        await db.SaveChangesAsync();

        var handler = new ListarClientesPaginadoQueryHandler(db, TenantTeste.Contexto());

        var result = await handler.Handle(
            new ListarClientesPaginadoQuery(Termo: null, Ativo: null, ComSaldoDevedor: true, ComEmprestimo: null, Pagina: 1, TamanhoPagina: 20),
            CancellationToken.None);

        var item = Assert.Single(result.Itens);
        Assert.Equal(comSaldo.Id, item.Id);
    }

    [Fact]
    public async Task Handle_FiltroComEmprestimo_DeveRetornarApenasQuemTemVasilhameEmAberto()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var maria = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        var joao = new Cliente("João Pereira", "11977776666", "Rua B, 2");
        var semEmprestimo = new Cliente("Carla Dias", "11944444444", "Rua C, 3");
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, controlaVasilhame: true);
        db.Clientes.AddRange(maria, joao, semEmprestimo);
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();

        // Maria: empréstimo aberto de 10 → deve entrar.
        db.EmprestimosVasilhames.Add(new EmprestimoVasilhame(maria.Id, produto.Id, 10, new DateTime(2026, 1, 5, 10, 0, 0, DateTimeKind.Utc)));
        // João: empréstimo de 4 com devolução registrada → quitado, não deve entrar.
        var quitado = new EmprestimoVasilhame(joao.Id, produto.Id, 4, new DateTime(2026, 1, 6, 10, 0, 0, DateTimeKind.Utc));
        quitado.RegistrarDevolucao(4);
        db.EmprestimosVasilhames.Add(quitado);
        await db.SaveChangesAsync();

        var handler = new ListarClientesPaginadoQueryHandler(db, TenantTeste.Contexto());

        var result = await handler.Handle(
            new ListarClientesPaginadoQuery(Termo: null, Ativo: null, ComSaldoDevedor: null, ComEmprestimo: true, Pagina: 1, TamanhoPagina: 20),
            CancellationToken.None);

        var item = Assert.Single(result.Itens);
        Assert.Equal(maria.Id, item.Id);
        Assert.Equal(10, item.QuantidadeVasilhamesEmprestados);
    }

    [Fact]
    public async Task Handle_FiltrosCombinados_DeveRetornarApenasQuemAtendeTodos()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        // Atende os três filtros: ativo, com saldo devedor e com vasilhame em aberto.
        var maria = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        maria.AdicionarSaldoDevedor(30m);
        // Só ativo e com vasilhame, sem saldo → deve ficar fora.
        var joao = new Cliente("João Pereira", "11977776666", "Rua B, 2");
        // Só com saldo e vasilhame, mas inativo → deve ficar fora.
        var carla = new Cliente("Carla Dias", "11944444444", "Rua C, 3");
        carla.AdicionarSaldoDevedor(20m);
        carla.Desativar();
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, controlaVasilhame: true);
        db.Clientes.AddRange(maria, joao, carla);
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();

        db.EmprestimosVasilhames.Add(new EmprestimoVasilhame(maria.Id, produto.Id, 10, new DateTime(2026, 1, 5, 10, 0, 0, DateTimeKind.Utc)));
        db.EmprestimosVasilhames.Add(new EmprestimoVasilhame(joao.Id, produto.Id, 5, new DateTime(2026, 1, 6, 10, 0, 0, DateTimeKind.Utc)));
        db.EmprestimosVasilhames.Add(new EmprestimoVasilhame(carla.Id, produto.Id, 5, new DateTime(2026, 1, 7, 10, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var handler = new ListarClientesPaginadoQueryHandler(db, TenantTeste.Contexto());

        var result = await handler.Handle(
            new ListarClientesPaginadoQuery(Termo: null, Ativo: true, ComSaldoDevedor: true, ComEmprestimo: true, Pagina: 1, TamanhoPagina: 20),
            CancellationToken.None);

        var item = Assert.Single(result.Itens);
        Assert.Equal(maria.Id, item.Id);
    }

    [Fact]
    public async Task Handle_TermoNulo_DeveRetornarTodosSemQuebrarBusca()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        db.Clientes.AddRange(
            new Cliente("Ana Paula", "11922222222", "Rua A, 2"),
            new Cliente("Bruno Lima", "11933333333", "Rua B, 3"));
        await db.SaveChangesAsync();

        var handler = new ListarClientesPaginadoQueryHandler(db, TenantTeste.Contexto());

        // Termo nulo: o filtro ILike precisa ser pulado (o provider InMemory não traduz ILike);
        // a busca por termo em si é coberta pelos testes de integração no Postgres real.
        var result = await handler.Handle(
            new ListarClientesPaginadoQuery(Termo: null, Ativo: null, ComSaldoDevedor: null, ComEmprestimo: null, Pagina: 1, TamanhoPagina: 20),
            CancellationToken.None);

        Assert.Equal(2, result.Total);
    }
}