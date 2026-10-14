using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Clientes.Emprestimos;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.UnitTests.Features.Clientes.Emprestimos;

public class RegistrarEmprestimoCommandHandlerTests
{
    private static async Task<(AppDbContext Db, Cliente Cliente, Produto Produto)> CriarAsync(
        bool controlaVasilhame = true, int vasilhamesDisponiveis = 10)
    {
        var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, controlaVasilhame: controlaVasilhame);
        if (vasilhamesDisponiveis > 0)
        {
            produto.RegistrarEntradaVasilhame(vasilhamesDisponiveis);
        }

        db.Clientes.Add(cliente);
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();
        return (db, cliente, produto);
    }

    [Fact]
    public async Task Handle_Admin_DeveCriarEmprestimoAberto_IncrementarEmprestados_ConsumirDisponiveisERegistrarHistorico()
    {
        var (db, cliente, produto) = await CriarAsync(vasilhamesDisponiveis: 10);
        var handler = new RegistrarEmprestimoCommandHandler(
            db,
            new FakeAuthenticationStateProvider("maria@teste.com"),
            new FakeUsuarioAutenticado(ehAdmin: true));

        var id = await handler.Handle(
            new RegistrarEmprestimoCommand(cliente.Id, produto.Id, 3, "Cliente levou para casa"),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);

        var emprestimo = Assert.Single(db.EmprestimosVasilhames);
        Assert.Equal(cliente.Id, emprestimo.ClienteId);
        Assert.Equal(produto.Id, emprestimo.ProdutoId);
        Assert.Equal(3, emprestimo.Quantidade);
        Assert.Equal(0, emprestimo.QuantidadeDevolvida);
        Assert.Equal(3, emprestimo.Saldo);
        Assert.Null(emprestimo.DevolvidoEm);
        Assert.Equal("Cliente levou para casa", emprestimo.Observacao);

        // O vazio pego do depósito (disponível) foi entregue ao cliente (emprestado).
        var produtoAtualizado = await db.Produtos.FindAsync(produto.Id);
        Assert.Equal(3, produtoAtualizado!.QuantidadeVasilhamesEmprestados);
        Assert.Equal(7, produtoAtualizado.QuantidadeVasilhames); // 10 − 3 consumidos

        var historico = Assert.Single(db.HistoricosEstoque);
        Assert.Equal(TipoHistoricoEstoque.EmprestimoVasilhame, historico.Tipo);
        Assert.Equal("Empréstimo de 3 vasilhames ao cliente", historico.Descricao);
        Assert.Equal(3, historico.Quantidade);
        Assert.Equal("maria@teste.com", historico.Usuario);
    }

    [Fact]
    public async Task Handle_SemVaziosDisponiveis_DeveLancar_E_NadaPersistido()
    {
        // O empréstimo manual exige vazio FÍSICO no depósito (RegistrarSaidaVasilhame lança se
        // insuficiente). Sem vazios, não cria empréstimo nem deixa estado parcial.
        var (db, cliente, produto) = await CriarAsync(vasilhamesDisponiveis: 0);
        var handler = new RegistrarEmprestimoCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: true));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new RegistrarEmprestimoCommand(cliente.Id, produto.Id, 2), CancellationToken.None).AsTask());

        Assert.Empty(db.EmprestimosVasilhames);
        Assert.Empty(db.HistoricosEstoque);

        var produtoAtualizado = await db.Produtos.FindAsync(produto.Id);
        Assert.Equal(0, produtoAtualizado!.QuantidadeVasilhamesEmprestados);
        Assert.Equal(0, produtoAtualizado.QuantidadeVasilhames);
    }

    [Fact]
    public async Task Handle_ComQuantidadeUm_DeveRegistrarDescricaoNoSingular()
    {
        var (db, cliente, produto) = await CriarAsync();
        var handler = new RegistrarEmprestimoCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: true));

        await handler.Handle(new RegistrarEmprestimoCommand(cliente.Id, produto.Id, 1), CancellationToken.None);

        var historico = Assert.Single(db.HistoricosEstoque);
        Assert.Equal("Empréstimo de 1 vasilhame ao cliente", historico.Descricao);
    }

    [Fact]
    public async Task Handle_SemProvider_DeveRegistrarHistoricoComUsuarioSistema()
    {
        var (db, cliente, produto) = await CriarAsync();
        var handler = new RegistrarEmprestimoCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: true));

        await handler.Handle(new RegistrarEmprestimoCommand(cliente.Id, produto.Id, 2), CancellationToken.None);

        var historico = Assert.Single(db.HistoricosEstoque);
        Assert.Equal("sistema", historico.Usuario);
        Assert.Equal("Empréstimo de 2 vasilhames ao cliente", historico.Descricao);
    }

    [Fact]
    public async Task Handle_DeveLancar_QuandoProdutoNaoControlaVasilhame()
    {
        var (db, cliente, produto) = await CriarAsync(controlaVasilhame: false);
        var handler = new RegistrarEmprestimoCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: true));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new RegistrarEmprestimoCommand(cliente.Id, produto.Id, 2), CancellationToken.None).AsTask());

        Assert.Empty(db.EmprestimosVasilhames);
        Assert.Empty(db.HistoricosEstoque);
    }

    [Fact]
    public async Task Handle_DeveLancar_QuandoProdutoNaoExiste()
    {
        var (db, cliente, _) = await CriarAsync();
        var handler = new RegistrarEmprestimoCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: true));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new RegistrarEmprestimoCommand(cliente.Id, Guid.NewGuid(), 2), CancellationToken.None).AsTask());

        Assert.Empty(db.EmprestimosVasilhames);
        Assert.Empty(db.HistoricosEstoque);
    }

    [Fact]
    public async Task Handle_DeveLancar_QuandoNaoAdmin()
    {
        var (db, cliente, produto) = await CriarAsync();
        var handler = new RegistrarEmprestimoCommandHandler(db, usuarioAutenticado: new FakeUsuarioAutenticado(ehAdmin: false));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new RegistrarEmprestimoCommand(cliente.Id, produto.Id, 2), CancellationToken.None).AsTask());

        Assert.Empty(db.EmprestimosVasilhames);
        Assert.Empty(db.HistoricosEstoque);
    }
}