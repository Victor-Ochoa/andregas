using RevendaApp.Domain.Entities;
using RevendaApp.Domain.Enums;
using RevendaApp.UnitTests.TestHelpers;
using RevendaApp.Web.Features.Estoque.Movimentar;

namespace RevendaApp.UnitTests.Features.Estoque;

public class RegistrarMovimentacaoEstoqueCommandHandlerTests
{
    private static async Task<(RevendaApp.Infrastructure.AppDbContext Db, Produto Produto)> CriarProdutoAsync(
        int estoqueInicial = 0, bool controlaVasilhame = false, int vasilhamesIniciais = 0)
    {
        var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, controlaVasilhame: controlaVasilhame);
        if (estoqueInicial > 0)
        {
            produto.RegistrarEntrada(estoqueInicial);
        }

        if (vasilhamesIniciais > 0)
        {
            produto.RegistrarEntradaVasilhame(vasilhamesIniciais);
        }

        db.Produtos.Add(produto);
        await db.SaveChangesAsync();
        return (db, produto);
    }

    [Fact]
    public async Task Handle_Entrada_DeveAumentarEstoqueECriarMovimentacao()
    {
        var (db, produto) = await CriarProdutoAsync();
        var handler = new RegistrarMovimentacaoEstoqueCommandHandler(db, TenantTeste.Contexto());

        var sucesso = await handler.Handle(new RegistrarMovimentacaoEstoqueCommand(produto.Id, TipoMovimentacaoEstoque.Entrada, 10, "Compra do fornecedor"), CancellationToken.None);

        Assert.True(sucesso);
        var atualizado = await db.Produtos.FindAsync(produto.Id);
        Assert.Equal(10, atualizado!.QuantidadeEstoque);

        var movimentacao = Assert.Single(db.MovimentacoesEstoque);
        Assert.Equal(TipoMovimentacaoEstoque.Entrada, movimentacao.Tipo);
        Assert.Equal(10, movimentacao.Quantidade);
        Assert.Equal("Compra do fornecedor", movimentacao.Motivo);
    }

    [Fact]
    public async Task Handle_Saida_DeveDiminuirEstoque()
    {
        var (db, produto) = await CriarProdutoAsync(estoqueInicial: 10);
        var handler = new RegistrarMovimentacaoEstoqueCommandHandler(db, TenantTeste.Contexto());

        var sucesso = await handler.Handle(new RegistrarMovimentacaoEstoqueCommand(produto.Id, TipoMovimentacaoEstoque.Saida, 4, null), CancellationToken.None);

        Assert.True(sucesso);
        var atualizado = await db.Produtos.FindAsync(produto.Id);
        Assert.Equal(6, atualizado!.QuantidadeEstoque);
    }

    [Fact]
    public async Task Handle_Saida_DeveLancarExcecao_QuandoEstoqueInsuficiente()
    {
        var (db, produto) = await CriarProdutoAsync(estoqueInicial: 2);
        var handler = new RegistrarMovimentacaoEstoqueCommandHandler(db, TenantTeste.Contexto());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new RegistrarMovimentacaoEstoqueCommand(produto.Id, TipoMovimentacaoEstoque.Saida, 5, null), CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Handle_Ajuste_DeveDefinirQuantidadeExata()
    {
        var (db, produto) = await CriarProdutoAsync(estoqueInicial: 10);
        var handler = new RegistrarMovimentacaoEstoqueCommandHandler(db, TenantTeste.Contexto());

        var sucesso = await handler.Handle(new RegistrarMovimentacaoEstoqueCommand(produto.Id, TipoMovimentacaoEstoque.Ajuste, 3, "Inventário"), CancellationToken.None);

        Assert.True(sucesso);
        var atualizado = await db.Produtos.FindAsync(produto.Id);
        Assert.Equal(3, atualizado!.QuantidadeEstoque);
    }

    [Fact]
    public async Task Handle_DeveRetornarFalso_QuandoProdutoNaoExiste()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);
        var handler = new RegistrarMovimentacaoEstoqueCommandHandler(db, TenantTeste.Contexto());

        var sucesso = await handler.Handle(new RegistrarMovimentacaoEstoqueCommand(Guid.NewGuid(), TipoMovimentacaoEstoque.Entrada, 5, null), CancellationToken.None);

        Assert.False(sucesso);
    }

    [Fact]
    public async Task Handle_Entrada_DeveRegistrarHistoricoComUsuario()
    {
        var (db, produto) = await CriarProdutoAsync();
        var authProvider = new FakeAuthenticationStateProvider("maria@teste.com");
        var handler = new RegistrarMovimentacaoEstoqueCommandHandler(db, TenantTeste.Contexto(), authProvider);

        await handler.Handle(new RegistrarMovimentacaoEstoqueCommand(produto.Id, TipoMovimentacaoEstoque.Entrada, 10, "Compra do fornecedor"), CancellationToken.None);

        var historico = Assert.Single(db.HistoricosEstoque);
        Assert.Equal(TipoHistoricoEstoque.Entrada, historico.Tipo);
        Assert.Equal(10, historico.Quantidade);
        Assert.Equal("Compra do fornecedor", historico.Motivo);
        Assert.Equal("maria@teste.com", historico.Usuario);
        Assert.Equal("Entrada de 10 produtos", historico.Descricao);
    }

    [Fact]
    public async Task Handle_SemProvider_DeveRegistrarHistoricoComUsuarioSistema()
    {
        var (db, produto) = await CriarProdutoAsync();
        var handler = new RegistrarMovimentacaoEstoqueCommandHandler(db, TenantTeste.Contexto());

        await handler.Handle(new RegistrarMovimentacaoEstoqueCommand(produto.Id, TipoMovimentacaoEstoque.Entrada, 10, null), CancellationToken.None);

        var historico = Assert.Single(db.HistoricosEstoque);
        Assert.Equal("sistema", historico.Usuario);
        Assert.Equal("Entrada de 10 produtos", historico.Descricao);
    }

    [Fact]
    public async Task Handle_Ajuste_DeveRegistrarHistoricoComQuantidadeAlvo()
    {
        var (db, produto) = await CriarProdutoAsync(estoqueInicial: 10);
        var handler = new RegistrarMovimentacaoEstoqueCommandHandler(db, TenantTeste.Contexto());

        await handler.Handle(new RegistrarMovimentacaoEstoqueCommand(produto.Id, TipoMovimentacaoEstoque.Ajuste, 3, "Inventário"), CancellationToken.None);

        var historico = Assert.Single(db.HistoricosEstoque);
        Assert.Equal(TipoHistoricoEstoque.Ajuste, historico.Tipo);
        Assert.Equal(3, historico.Quantidade);
        Assert.Equal("Ajuste de estoque para 3", historico.Descricao);
    }

    [Fact]
    public async Task Handle_Saida_DeveRegistrarHistorico()
    {
        var (db, produto) = await CriarProdutoAsync(estoqueInicial: 10);
        var handler = new RegistrarMovimentacaoEstoqueCommandHandler(db, TenantTeste.Contexto());

        await handler.Handle(new RegistrarMovimentacaoEstoqueCommand(produto.Id, TipoMovimentacaoEstoque.Saida, 4, null), CancellationToken.None);

        var historico = Assert.Single(db.HistoricosEstoque);
        Assert.Equal(TipoHistoricoEstoque.Saida, historico.Tipo);
        Assert.Equal(4, historico.Quantidade);
        Assert.Equal("Saída de 4 produtos", historico.Descricao);
    }

    [Fact]
    public async Task Handle_Entrada_ProdutoComVasilhame_DeveAumentarEstoque_E_ConsumirVasilhamesDoDeposito()
    {
        // Nova semântica (decisão do usuário): comprar N cheios entrega N vazios ao fornecedor.
        // Estoque cheio +N (10) e Vasilhames disponíveis −N (de 20 para 10).
        var (db, produto) = await CriarProdutoAsync(controlaVasilhame: true, vasilhamesIniciais: 20);
        var handler = new RegistrarMovimentacaoEstoqueCommandHandler(db, TenantTeste.Contexto());

        var sucesso = await handler.Handle(new RegistrarMovimentacaoEstoqueCommand(produto.Id, TipoMovimentacaoEstoque.Entrada, 10, null), CancellationToken.None);

        Assert.True(sucesso);
        var atualizado = await db.Produtos.FindAsync(produto.Id);
        Assert.Equal(10, atualizado!.QuantidadeEstoque);
        Assert.Equal(10, atualizado.QuantidadeVasilhames); // 20 − 10 entregues ao fornecedor

        var historicoVasilhame = Assert.Single(db.HistoricosEstoque.Where(h => h.Tipo == TipoHistoricoEstoque.Vasilhame));
        Assert.Contains("Entrega de 10 vasilhames ao fornecedor", historicoVasilhame.Descricao);
        Assert.Equal(10, historicoVasilhame.Quantidade);
        Assert.Equal(2, db.HistoricosEstoque.Count()); // entrada dos produtos + entrega de vazios
    }

    [Fact]
    public async Task Handle_Entrada_ProdutoComVasilhame_SemVaziosDisponiveis_DeveLancarSemPersistirNada()
    {
        // Compra de produto com controle sem vazios no depósito para entregar ao fornecedor → lança,
        // e nada é persistido (guard antes da mutação).
        var (db, produto) = await CriarProdutoAsync(controlaVasilhame: true, vasilhamesIniciais: 0);
        var handler = new RegistrarMovimentacaoEstoqueCommandHandler(db, TenantTeste.Contexto());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new RegistrarMovimentacaoEstoqueCommand(produto.Id, TipoMovimentacaoEstoque.Entrada, 10, null), CancellationToken.None).AsTask());

        var atualizado = await db.Produtos.FindAsync(produto.Id);
        Assert.Equal(0, atualizado!.QuantidadeEstoque); // nada persistido após a exceção
        Assert.Empty(db.MovimentacoesEstoque);
        Assert.Empty(db.HistoricosEstoque);
    }

    [Fact]
    public async Task Handle_Entrada_ProdutoSemControle_DeveAumentarEstoque_SemMexerEmVasilhames()
    {
        var (db, produto) = await CriarProdutoAsync(controlaVasilhame: false);
        var handler = new RegistrarMovimentacaoEstoqueCommandHandler(db, TenantTeste.Contexto());

        var sucesso = await handler.Handle(new RegistrarMovimentacaoEstoqueCommand(produto.Id, TipoMovimentacaoEstoque.Entrada, 10, null), CancellationToken.None);

        Assert.True(sucesso);
        var atualizado = await db.Produtos.FindAsync(produto.Id);
        Assert.Equal(10, atualizado!.QuantidadeEstoque);
        Assert.Equal(0, atualizado.QuantidadeVasilhames);
        Assert.Single(db.HistoricosEstoque); // apenas a entrada de produtos, sem histórico de vasilhame
    }

    [Fact]
    public async Task Handle_Saida_ProdutoComVasilhame_DeveDiminuirEstoque_SemMexerEmVasilhames()
    {
        // Saída manual (não venda): o produto sai com o vasilhame junto; o vazio NÃO volta ao
        // depósito (a troca/empréstimo é tratada na venda pelo RegistrarVendaCommandHandler).
        var (db, produto) = await CriarProdutoAsync(estoqueInicial: 10, controlaVasilhame: true, vasilhamesIniciais: 4);
        var handler = new RegistrarMovimentacaoEstoqueCommandHandler(db, TenantTeste.Contexto());

        var sucesso = await handler.Handle(new RegistrarMovimentacaoEstoqueCommand(produto.Id, TipoMovimentacaoEstoque.Saida, 4, null), CancellationToken.None);

        Assert.True(sucesso);
        var atualizado = await db.Produtos.FindAsync(produto.Id);
        Assert.Equal(6, atualizado!.QuantidadeEstoque);
        Assert.Equal(4, atualizado.QuantidadeVasilhames); // intacto
        Assert.Single(db.HistoricosEstoque); // só o histórico da saída, sem histórico de vasilhame
    }

    [Fact]
    public async Task Handle_Ajuste_ProdutoComVasilhame_NaoDeveTocarVasilhames()
    {
        var (db, produto) = await CriarProdutoAsync(estoqueInicial: 10, controlaVasilhame: true, vasilhamesIniciais: 3);
        var handler = new RegistrarMovimentacaoEstoqueCommandHandler(db, TenantTeste.Contexto());

        var sucesso = await handler.Handle(new RegistrarMovimentacaoEstoqueCommand(produto.Id, TipoMovimentacaoEstoque.Ajuste, 3, "Inventário"), CancellationToken.None);

        Assert.True(sucesso);
        var atualizado = await db.Produtos.FindAsync(produto.Id);
        Assert.Equal(3, atualizado!.QuantidadeEstoque);
        Assert.Equal(3, atualizado.QuantidadeVasilhames);
        Assert.Equal(0, atualizado.QuantidadeVasilhamesEmprestados);
        Assert.Single(db.HistoricosEstoque); // apenas o ajuste, sem histórico de vasilhame
    }
}