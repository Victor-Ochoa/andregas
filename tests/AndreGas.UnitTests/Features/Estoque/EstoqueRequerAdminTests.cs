using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Estoque.Cadastrar;
using AndreGas.Web.Features.Estoque.Editar;
using AndreGas.Web.Features.Estoque.Movimentar;

namespace AndreGas.UnitTests.Features.Estoque;

/// <summary>
/// Os handlers de mutação de estoque são restritos a administradores (a guarda via
/// <see cref="AndreGas.Web.Common.Authorization.IUsuarioAutenticado"/> é o limite real no servidor).
/// Estes testes garantem que um usuário não-admin recebe <see cref="InvalidOperationException"/>
/// e nenhuma alteração é persistida.
/// </summary>
public class EstoqueRequerAdminTests
{
    [Fact]
    public async Task CadastrarProduto_DeveLancar_QuandoUsuarioNaoAdmin()
    {
        using var db = InMemoryDbContextFactory.Create();
        var handler = new CadastrarProdutoCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: false));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(
                new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5),
                CancellationToken.None).AsTask());

        Assert.Empty(db.Produtos);
        Assert.Empty(db.HistoricosEstoque);
    }

    [Fact]
    public async Task CadastrarProduto_DeveExecutar_QuandoUsuarioAdmin()
    {
        using var db = InMemoryDbContextFactory.Create();
        var handler = new CadastrarProdutoCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: true));

        var id = await handler.Handle(
            new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
        Assert.Single(db.Produtos);
    }

    [Fact]
    public async Task AtualizarProduto_DeveLancar_QuandoUsuarioNaoAdmin()
    {
        using var db = InMemoryDbContextFactory.Create();
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, estoqueMinimo: 5);
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();

        var handler = new AtualizarProdutoCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: false));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(
                new AtualizarProdutoCommand(produto.Id, "Botijão Novo", TipoProduto.Gas, 100m, 60m, 80m, 5, Ativo: true),
                CancellationToken.None).AsTask());

        Assert.Equal("Botijão 13kg", db.Produtos.Find(produto.Id)!.Nome); // nada mudou
        Assert.Empty(db.HistoricosEstoque);
    }

    [Fact]
    public async Task RegistrarMovimentacao_DeveLancar_QuandoUsuarioNaoAdmin()
    {
        using var db = InMemoryDbContextFactory.Create();
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, estoqueMinimo: 5);
        produto.RegistrarEntrada(10);
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();

        var handler = new RegistrarMovimentacaoEstoqueCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: false));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(
                new RegistrarMovimentacaoEstoqueCommand(produto.Id, TipoMovimentacaoEstoque.Saida, 3, "Venda"),
                CancellationToken.None).AsTask());

        // Estoque inalterado e nenhuma movimentação/histórico registrado.
        Assert.Equal(10, db.Produtos.Find(produto.Id)!.QuantidadeEstoque);
        Assert.Empty(db.MovimentacoesEstoque);
        Assert.Empty(db.HistoricosEstoque);
    }
}