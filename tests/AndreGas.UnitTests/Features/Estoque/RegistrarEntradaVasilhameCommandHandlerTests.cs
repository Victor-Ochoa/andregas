using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Estoque.Vasilhames;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.UnitTests.Features.Estoque;

public class RegistrarEntradaVasilhameCommandHandlerTests
{
    private static async Task<(AppDbContext Db, Produto Produto)> CriarProdutoAsync(bool controlaVasilhame = true)
    {
        var db = InMemoryDbContextFactory.Create();
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, controlaVasilhame: controlaVasilhame);
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();
        return (db, produto);
    }

    [Fact]
    public async Task Handle_Admin_DeveIncrementarDisponiveis_ERegistrarHistorico()
    {
        var (db, produto) = await CriarProdutoAsync();
        var handler = new RegistrarEntradaVasilhameCommandHandler(
            db,
            new FakeAuthenticationStateProvider("maria@teste.com"),
            new FakeUsuarioAutenticado(ehAdmin: true));

        var sucesso = await handler.Handle(
            new RegistrarEntradaVasilhameCommand(produto.Id, 5, "Vazios recolhidos na rota"),
            CancellationToken.None);

        Assert.True(sucesso);

        var atualizado = await db.Produtos.FindAsync(produto.Id);
        Assert.Equal(5, atualizado!.QuantidadeVasilhames);
        Assert.Equal(0, atualizado.QuantidadeVasilhamesEmprestados);

        var historico = Assert.Single(db.HistoricosEstoque);
        Assert.Equal(TipoHistoricoEstoque.Vasilhame, historico.Tipo);
        Assert.Equal("Entrada de 5 vasilhames vazios ao depósito", historico.Descricao);
        Assert.Equal(5, historico.Quantidade);
        Assert.Equal("Vazios recolhidos na rota", historico.Motivo);
        Assert.Equal("maria@teste.com", historico.Usuario);
    }

    [Fact]
    public async Task Handle_QuantidadeUm_DeveRegistrarDescricaoNoSingular()
    {
        var (db, produto) = await CriarProdutoAsync();
        var handler = new RegistrarEntradaVasilhameCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: true));

        await handler.Handle(new RegistrarEntradaVasilhameCommand(produto.Id, 1), CancellationToken.None);

        var historico = Assert.Single(db.HistoricosEstoque);
        Assert.Equal("Entrada de 1 vasilhame vazio ao depósito", historico.Descricao);
    }

    [Fact]
    public async Task Handle_SemProvider_DeveRegistrarUsuarioSistema()
    {
        var (db, produto) = await CriarProdutoAsync();
        var handler = new RegistrarEntradaVasilhameCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: true));

        await handler.Handle(new RegistrarEntradaVasilhameCommand(produto.Id, 2), CancellationToken.None);

        var historico = Assert.Single(db.HistoricosEstoque);
        Assert.Equal("sistema", historico.Usuario);
    }

    [Fact]
    public async Task Handle_DeveLancar_QuandoProdutoNaoControlaVasilhame()
    {
        var (db, produto) = await CriarProdutoAsync(controlaVasilhame: false);
        var handler = new RegistrarEntradaVasilhameCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: true));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new RegistrarEntradaVasilhameCommand(produto.Id, 2), CancellationToken.None).AsTask());

        Assert.Empty(db.HistoricosEstoque);

        var atualizado = await db.Produtos.FindAsync(produto.Id);
        Assert.Equal(0, atualizado!.QuantidadeVasilhames);
    }

    [Fact]
    public async Task Handle_DeveLancar_QuandoProdutoNaoExiste()
    {
        using var db = InMemoryDbContextFactory.Create();
        var handler = new RegistrarEntradaVasilhameCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: true));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new RegistrarEntradaVasilhameCommand(Guid.NewGuid(), 2), CancellationToken.None).AsTask());

        Assert.Empty(db.HistoricosEstoque);
    }

    [Fact]
    public async Task Handle_DeveLancar_QuandoNaoAdmin()
    {
        var (db, produto) = await CriarProdutoAsync();
        var handler = new RegistrarEntradaVasilhameCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: false));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new RegistrarEntradaVasilhameCommand(produto.Id, 2), CancellationToken.None).AsTask());

        Assert.Empty(db.HistoricosEstoque);

        var atualizado = await db.Produtos.FindAsync(produto.Id);
        Assert.Equal(0, atualizado!.QuantidadeVasilhames);
    }
}