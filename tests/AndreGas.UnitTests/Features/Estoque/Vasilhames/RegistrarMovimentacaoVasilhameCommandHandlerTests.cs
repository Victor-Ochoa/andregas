using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Estoque.Vasilhames;

namespace AndreGas.UnitTests.Features.Estoque.Vasilhames;

public class RegistrarMovimentacaoVasilhameCommandHandlerTests
{
    private static async Task<(AppDbContext Db, Produto Produto)> CriarProdutoAsync(bool controlaVasilhame = true)
    {
        var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, controlaVasilhame: controlaVasilhame);
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();
        return (db, produto);
    }

    private static RegistrarMovimentacaoVasilhameCommandHandler CriarHandler(AppDbContext db, bool ehAdmin = true) =>
        new(db, TenantTeste.Contexto(), usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: ehAdmin));

    [Fact]
    public async Task Handle_Entrada_DeveSomarAosDisponiveis_ERegistrarHistorico()
    {
        var (db, produto) = await CriarProdutoAsync();
        var handler = CriarHandler(db);

        var sucesso = await handler.Handle(
            new RegistrarMovimentacaoVasilhameCommand(produto.Id, TipoMovimentacaoEstoque.Entrada, 5, "Vazios recolhidos na rota"),
            CancellationToken.None);

        Assert.True(sucesso);

        var atualizado = await db.Produtos.FindAsync([produto.Id]);
        Assert.Equal(5, atualizado!.QuantidadeVasilhames);
        Assert.Equal(0, atualizado.QuantidadeVasilhamesEmprestados);

        var historico = Assert.Single(db.HistoricosEstoque);
        Assert.Equal(TipoHistoricoEstoque.Vasilhame, historico.Tipo);
        Assert.Equal("Entrada de 5 vasilhames vazios ao depósito", historico.Descricao);
        Assert.Equal(5, historico.Quantidade);
        Assert.Equal("Vazios recolhidos na rota", historico.Motivo);
        Assert.Equal("sistema", historico.Usuario);
    }

    [Fact]
    public async Task Handle_Saida_DeveDiminuirOsDisponiveis()
    {
        var (db, produto) = await CriarProdutoAsync();
        produto.RegistrarEntradaVasilhame(8);
        await db.SaveChangesAsync();
        var handler = CriarHandler(db);

        await handler.Handle(
            new RegistrarMovimentacaoVasilhameCommand(produto.Id, TipoMovimentacaoEstoque.Saida, 3),
            CancellationToken.None);

        var atualizado = await db.Produtos.FindAsync([produto.Id]);
        Assert.Equal(5, atualizado!.QuantidadeVasilhames);
        Assert.Equal(0, atualizado.QuantidadeVasilhamesEmprestados);

        var historico = Assert.Single(db.HistoricosEstoque);
        Assert.Equal("Saída de 3 vasilhames vazios do depósito", historico.Descricao);
    }

    [Fact]
    public async Task Handle_Saida_DeveLancar_QuandoDisponiveisInsuficientes()
    {
        var (db, produto) = await CriarProdutoAsync();
        produto.RegistrarEntradaVasilhame(2);
        await db.SaveChangesAsync();
        var handler = CriarHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(
                new RegistrarMovimentacaoVasilhameCommand(produto.Id, TipoMovimentacaoEstoque.Saida, 3),
                CancellationToken.None).AsTask());

        var atualizado = await db.Produtos.FindAsync([produto.Id]);
        Assert.Equal(2, atualizado!.QuantidadeVasilhames); // inalterado
        Assert.Empty(db.HistoricosEstoque);
    }

    [Fact]
    public async Task Handle_Ajuste_DeveDefinirTotalExatoDeDisponiveis()
    {
        var (db, produto) = await CriarProdutoAsync();
        produto.RegistrarEntradaVasilhame(7);
        await db.SaveChangesAsync();
        var handler = CriarHandler(db);

        await handler.Handle(
            new RegistrarMovimentacaoVasilhameCommand(produto.Id, TipoMovimentacaoEstoque.Ajuste, 2),
            CancellationToken.None);

        var atualizado = await db.Produtos.FindAsync([produto.Id]);
        Assert.Equal(2, atualizado!.QuantidadeVasilhames);
        Assert.Equal(0, atualizado.QuantidadeVasilhamesEmprestados);

        var historico = Assert.Single(db.HistoricosEstoque);
        Assert.Equal("Ajuste de vasilhames para 2 disponíveis", historico.Descricao);
    }

    [Fact]
    public async Task Handle_Ajuste_DevePermitirZerarDisponiveis()
    {
        var (db, produto) = await CriarProdutoAsync();
        produto.RegistrarEntradaVasilhame(3);
        await db.SaveChangesAsync();
        var handler = CriarHandler(db);

        await handler.Handle(
            new RegistrarMovimentacaoVasilhameCommand(produto.Id, TipoMovimentacaoEstoque.Ajuste, 0),
            CancellationToken.None);

        var atualizado = await db.Produtos.FindAsync([produto.Id]);
        Assert.Equal(0, atualizado!.QuantidadeVasilhames);
    }

    [Fact]
    public async Task Handle_DeveLancar_QuandoProdutoNaoControlaVasilhame()
    {
        var (db, produto) = await CriarProdutoAsync(controlaVasilhame: false);
        var handler = CriarHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(
                new RegistrarMovimentacaoVasilhameCommand(produto.Id, TipoMovimentacaoEstoque.Entrada, 2),
                CancellationToken.None).AsTask());

        Assert.Empty(db.HistoricosEstoque);
        var atualizado = await db.Produtos.FindAsync([produto.Id]);
        Assert.Equal(0, atualizado!.QuantidadeVasilhames);
    }

    [Fact]
    public async Task Handle_DeveLancar_QuandoProdutoNaoExiste()
    {
        var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var handler = CriarHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(
                new RegistrarMovimentacaoVasilhameCommand(Guid.NewGuid(), TipoMovimentacaoEstoque.Entrada, 2),
                CancellationToken.None).AsTask());

        Assert.Empty(db.HistoricosEstoque);
    }

    [Fact]
    public async Task Handle_DeveLancar_QuandoNaoAdmin()
    {
        var (db, produto) = await CriarProdutoAsync();
        var handler = CriarHandler(db, ehAdmin: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(
                new RegistrarMovimentacaoVasilhameCommand(produto.Id, TipoMovimentacaoEstoque.Entrada, 2),
                CancellationToken.None).AsTask());

        var atualizado = await db.Produtos.FindAsync([produto.Id]);
        Assert.Equal(0, atualizado!.QuantidadeVasilhames);
        Assert.Empty(db.HistoricosEstoque);
    }

    [Fact]
    public async Task Handle_ComUsuarioAutenticado_DeveRegistrarNomeNoHistorico()
    {
        var (db, produto) = await CriarProdutoAsync();
        var authProvider = new FakeAuthenticationStateProvider("maria@teste.com");
        var handler = new RegistrarMovimentacaoVasilhameCommandHandler(db, TenantTeste.Contexto(), authProvider, new FakeUsuarioAutenticado(ehAdmin: true));

        await handler.Handle(
            new RegistrarMovimentacaoVasilhameCommand(produto.Id, TipoMovimentacaoEstoque.Entrada, 1),
            CancellationToken.None);

        var historico = Assert.Single(db.HistoricosEstoque);
        Assert.Equal("maria@teste.com", historico.Usuario);
        Assert.Equal("Entrada de 1 vasilhame vazio ao depósito", historico.Descricao); // singular
    }
}