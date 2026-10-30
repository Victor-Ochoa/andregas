using RevendaApp.Domain.Enums;
using RevendaApp.UnitTests.TestHelpers;
using RevendaApp.Web.Features.Estoque.Cadastrar;

namespace RevendaApp.UnitTests.Features.Estoque;

public class CadastrarProdutoCommandHandlerTests
{
    [Fact]
    public async Task Handle_DeveCriarProdutoComEstoqueZerado()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var handler = new CadastrarProdutoCommandHandler(db, TenantTeste.Contexto());

        var id = await handler.Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);

        var produto = await db.Produtos.FindAsync(id);
        Assert.NotNull(produto);
        Assert.Equal("Botijão 13kg", produto!.Nome);
        Assert.Equal(0, produto.QuantidadeEstoque);
        Assert.Equal(100m, produto.PrecoVenda);
        Assert.Equal(80m, produto.PrecoGasDoPovo);
    }

    [Fact]
    public async Task Handle_DeveRegistrarHistoricoDeCadastroInicial()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var handler = new CadastrarProdutoCommandHandler(db, TenantTeste.Contexto());

        var id = await handler.Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);

        var historico = Assert.Single(db.HistoricosEstoque);
        Assert.Equal(id, historico.ProdutoId);
        Assert.Equal(TipoHistoricoEstoque.Cadastro, historico.Tipo);
        Assert.Equal("Cadastro inicial", historico.Descricao);
        Assert.Equal("Cadastro inicial", historico.Motivo);
        Assert.Equal("sistema", historico.Usuario);
    }

    [Fact]
    public async Task Handle_ComUsuarioAutenticado_DeveRegistrarNomeNoHistorico()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var authProvider = new FakeAuthenticationStateProvider("maria@teste.com");
        var handler = new CadastrarProdutoCommandHandler(db, TenantTeste.Contexto(), authProvider);

        var id = await handler.Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);

        var historico = Assert.Single(db.HistoricosEstoque);
        Assert.Equal("maria@teste.com", historico.Usuario);
    }

    [Fact]
    public async Task Handle_DevePersistirControleDeVasilhame_QuandoTrue()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var handler = new CadastrarProdutoCommandHandler(db, TenantTeste.Contexto());

        var id = await handler.Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5, ControlaVasilhame: true), CancellationToken.None);

        var produto = await db.Produtos.FindAsync(id);
        Assert.NotNull(produto);
        Assert.True(produto!.ControlaVasilhame);
    }

    [Fact]
    public async Task Handle_DeveIniciarComVasilhamesDisponiveis_QuandoInformadaQuantidadeInicial()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var handler = new CadastrarProdutoCommandHandler(db, TenantTeste.Contexto());

        var id = await handler.Handle(new CadastrarProdutoCommand(
            "Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5,
            ControlaVasilhame: true, QuantidadeVasilhamesInicial: 7), CancellationToken.None);

        var produto = await db.Produtos.FindAsync([id]);
        Assert.NotNull(produto);
        Assert.Equal(7, produto!.QuantidadeVasilhames);
        Assert.Equal(0, produto.QuantidadeVasilhamesEmprestados);

        // Dois históricos: cadastro com menção da entrada + histórico dedicado de vasilhame.
        var historicos = db.HistoricosEstoque.ToList();
        Assert.Equal(2, historicos.Count);
        Assert.Contains(historicos, h =>
            h.Tipo == TipoHistoricoEstoque.Cadastro && h.Descricao?.Contains("entrada de 7 vasilhames") == true);
        Assert.Contains(historicos, h =>
            h.Tipo == TipoHistoricoEstoque.Vasilhame && h.Quantidade == 7);
    }

    [Fact]
    public async Task Handle_SemQuantidadeInicial_NaoCriaHistoricoDeVasilhame()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var handler = new CadastrarProdutoCommandHandler(db, TenantTeste.Contexto());

        var id = await handler.Handle(new CadastrarProdutoCommand(
            "Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5,
            ControlaVasilhame: true), CancellationToken.None);

        var produto = await db.Produtos.FindAsync([id]);
        Assert.Equal(0, produto!.QuantidadeVasilhames);
        Assert.Single(db.HistoricosEstoque); // apenas o cadastro
    }

    [Fact]
    public async Task Handle_DevePersistirControleDeVasilhameFalso_QuandoFlagOmisso()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var handler = new CadastrarProdutoCommandHandler(db, TenantTeste.Contexto());

        var id = await handler.Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);

        var produto = await db.Produtos.FindAsync(id);
        Assert.NotNull(produto);
        Assert.False(produto!.ControlaVasilhame);
    }

    [Fact]
    public async Task Handle_DevePersistirAvisarRecompra_EDiasPadraoRecompra()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var handler = new CadastrarProdutoCommandHandler(db, TenantTeste.Contexto());

        var id = await handler.Handle(new CadastrarProdutoCommand(
            "Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5,
            ControlaVasilhame: true, AvisarRecompra: true, DiasPadraoRecompra: 45), CancellationToken.None);

        var produto = await db.Produtos.FindAsync([id]);
        Assert.True(produto!.AvisarRecompra);
        Assert.Equal(45, produto.DiasPadraoRecompra);
    }
}