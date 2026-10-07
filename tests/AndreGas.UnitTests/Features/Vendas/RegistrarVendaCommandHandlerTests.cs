using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure.Identity;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Vendas.NovaVenda;

namespace AndreGas.UnitTests.Features.Vendas;

public class RegistrarVendaCommandHandlerTests
{
    private static async Task<(AndreGas.Infrastructure.AppDbContext Db, Produto Produto)> CriarProdutoAsync(int estoque = 20)
    {
        var db = InMemoryDbContextFactory.Create();
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m);
        produto.RegistrarEntrada(estoque);
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();
        return (db, produto);
    }

    [Fact]
    public async Task Handle_DeveCriarClienteNovo_QuandoTelefoneNaoCadastrado()
    {
        var (db, produto) = await CriarProdutoAsync();
        var handler = new RegistrarVendaCommandHandler(db);

        var result = await handler.Handle(
            new RegistrarVendaCommand("11988887777", "Maria Souza", "Rua A, 1", FormaPagamento.Dinheiro, 0m,
                [new ItemVendaInput(produto.Id, 2)]),
            CancellationToken.None);

        var cliente = await db.Clientes.FindAsync(result.ClienteId);
        Assert.NotNull(cliente);
        Assert.Equal("Maria Souza", cliente!.Nome);
        Assert.Equal("11988887777", cliente.Telefone);
        Assert.Equal(0m, cliente.SaldoDevedor); // não é fiado
    }

    [Fact]
    public async Task Handle_DeveReutilizarClienteExistente_QuandoTelefoneJaCadastrado()
    {
        var (db, produto) = await CriarProdutoAsync();
        var clienteExistente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(clienteExistente);
        await db.SaveChangesAsync();

        var handler = new RegistrarVendaCommandHandler(db);
        var result = await handler.Handle(
            new RegistrarVendaCommand("(11) 98888-7777", null, null, FormaPagamento.Dinheiro, 0m,
                [new ItemVendaInput(produto.Id, 1)]),
            CancellationToken.None);

        Assert.Equal(clienteExistente.Id, result.ClienteId);
        Assert.Equal(1, db.Clientes.Count());
    }

    [Fact]
    public async Task Handle_DeveBaixarEstoqueECriarMovimentacaoDeSaida()
    {
        var (db, produto) = await CriarProdutoAsync(estoque: 20);
        var handler = new RegistrarVendaCommandHandler(db);

        await handler.Handle(
            new RegistrarVendaCommand("11988887777", "Maria Souza", "Rua A, 1", FormaPagamento.Dinheiro, 0m,
                [new ItemVendaInput(produto.Id, 5)]),
            CancellationToken.None);

        var produtoAtualizado = await db.Produtos.FindAsync(produto.Id);
        Assert.Equal(15, produtoAtualizado!.QuantidadeEstoque);

        var movimentacao = Assert.Single(db.MovimentacoesEstoque);
        Assert.Equal(TipoMovimentacaoEstoque.Saida, movimentacao.Tipo);
        Assert.Equal(5, movimentacao.Quantidade);
    }

    [Fact]
    public async Task Handle_DeveCalcularValorTotalELucroTotalCorretamente()
    {
        // Botijão: venda 100, custo 60 -> lucro bruto 40/unidade.
        var (db, produto) = await CriarProdutoAsync();
        var handler = new RegistrarVendaCommandHandler(db);

        var result = await handler.Handle(
            new RegistrarVendaCommand("11988887777", "Maria Souza", "Rua A, 1", FormaPagamento.Dinheiro, 0m,
                [new ItemVendaInput(produto.Id, 3)]),
            CancellationToken.None);

        Assert.Equal(300m, result.ValorTotal);
        Assert.Equal(120m, result.LucroTotal);
    }

    [Fact]
    public async Task Handle_DeveAplicarDescontoReduzindoValorTotalELucro()
    {
        var (db, produto) = await CriarProdutoAsync();
        var handler = new RegistrarVendaCommandHandler(db);

        var result = await handler.Handle(
            new RegistrarVendaCommand("11988887777", "Maria Souza", "Rua A, 1", FormaPagamento.Dinheiro, 20m,
                [new ItemVendaInput(produto.Id, 2)]),
            CancellationToken.None);

        Assert.Equal(180m, result.ValorTotal); // 200 - 20
        Assert.Equal(60m, result.LucroTotal);  // 80 lucro bruto - 20 desconto
    }

    [Fact]
    public async Task Handle_DeveUsarPrecoGasDoPovo_QuandoFormaPagamentoForGasDoPovo()
    {
        var (db, produto) = await CriarProdutoAsync();
        var handler = new RegistrarVendaCommandHandler(db);

        var result = await handler.Handle(
            new RegistrarVendaCommand("11988887777", "Maria Souza", "Rua A, 1", FormaPagamento.GasDoPovo, 0m,
                [new ItemVendaInput(produto.Id, 1)]),
            CancellationToken.None);

        Assert.Equal(80m, result.ValorTotal); // PrecoGasDoPovo em vez de PrecoVenda
    }

    [Fact]
    public async Task Handle_DeveSomarAoSaldoDevedor_QuandoFiado()
    {
        var (db, produto) = await CriarProdutoAsync();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var handler = new RegistrarVendaCommandHandler(db);
        await handler.Handle(
            new RegistrarVendaCommand("11988887777", null, null, FormaPagamento.Fiado, 0m,
                [new ItemVendaInput(produto.Id, 1)]),
            CancellationToken.None);

        var clienteAtualizado = await db.Clientes.FindAsync(cliente.Id);
        Assert.Equal(100m, clienteAtualizado!.SaldoDevedor);
    }

    [Theory]
    [InlineData(FormaPagamento.Pix)]
    [InlineData(FormaPagamento.Debito)]
    [InlineData(FormaPagamento.Credito)]
    [InlineData(FormaPagamento.Dinheiro)]
    [InlineData(FormaPagamento.GasDoPovo)]
    public async Task Handle_NaoDeveAlterarSaldoDevedor_QuandoFormaNaoForFiado(FormaPagamento forma)
    {
        var (db, produto) = await CriarProdutoAsync();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var handler = new RegistrarVendaCommandHandler(db);
        await handler.Handle(
            new RegistrarVendaCommand("11988887777", null, null, forma, 0m,
                [new ItemVendaInput(produto.Id, 1)]),
            CancellationToken.None);

        var clienteAtualizado = await db.Clientes.FindAsync(cliente.Id);
        Assert.Equal(0m, clienteAtualizado!.SaldoDevedor);
    }

    [Theory]
    [InlineData(FormaPagamento.Pix)]
    [InlineData(FormaPagamento.Debito)]
    [InlineData(FormaPagamento.Credito)]
    [InlineData(FormaPagamento.Dinheiro)]
    [InlineData(FormaPagamento.GasDoPovo)]
    public async Task Handle_DeveRegistrarPagamento_QuandoFormaNaoForFiado(FormaPagamento forma)
    {
        // Botijão: venda 100, custo 60 -> ValorTotal 100.
        var (db, produto) = await CriarProdutoAsync();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var handler = new RegistrarVendaCommandHandler(db);
        var result = await handler.Handle(
            new RegistrarVendaCommand("11988887777", null, null, forma, 0m,
                [new ItemVendaInput(produto.Id, 1)]),
            CancellationToken.None);

        var pagamento = Assert.Single(db.Pagamentos);
        Assert.Equal(cliente.Id, pagamento.ClienteId);
        Assert.Equal(result.ValorTotal, pagamento.Valor);
        Assert.Equal(forma, pagamento.FormaPagamento);
    }

    [Fact]
    public async Task Handle_DeveRegistrarPagamentoComValorLiquidoDoDesconto_QuandoNaoForFiado()
    {
        var (db, produto) = await CriarProdutoAsync();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var handler = new RegistrarVendaCommandHandler(db);
        var result = await handler.Handle(
            new RegistrarVendaCommand("11988887777", null, null, FormaPagamento.Dinheiro, 20m,
                [new ItemVendaInput(produto.Id, 2)]),
            CancellationToken.None);

        var pagamento = Assert.Single(db.Pagamentos);
        Assert.Equal(180m, result.ValorTotal); // 200 - 20
        Assert.Equal(180m, pagamento.Valor);
    }

    [Fact]
    public async Task Handle_NaoDeveRegistrarPagamento_QuandoFiado()
    {
        var (db, produto) = await CriarProdutoAsync();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var handler = new RegistrarVendaCommandHandler(db);
        await handler.Handle(
            new RegistrarVendaCommand("11988887777", null, null, FormaPagamento.Fiado, 0m,
                [new ItemVendaInput(produto.Id, 1)]),
            CancellationToken.None);

        Assert.Empty(db.Pagamentos);
    }

    [Fact]
    public async Task Handle_ComValorEntrega_DeveIncluirNoValorTotalEResultado()
    {
        var (db, produto) = await CriarProdutoAsync();
        var handler = new RegistrarVendaCommandHandler(db);

        var result = await handler.Handle(
            new RegistrarVendaCommand("11988887777", "Maria Souza", "Rua A, 1", FormaPagamento.Dinheiro, 0m,
                [new ItemVendaInput(produto.Id, 3)], ValorEntrega: 15m),
            CancellationToken.None);

        Assert.Equal(315m, result.ValorTotal); // 300 + 15 de entrega
        Assert.Equal(120m, result.LucroTotal); // entrega não altera o lucro
    }

    [Fact]
    public async Task Handle_ComValorEntrega_Fiado_DeveSomarEntregaAoSaldoDevedor()
    {
        var (db, produto) = await CriarProdutoAsync();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var handler = new RegistrarVendaCommandHandler(db);
        await handler.Handle(
            new RegistrarVendaCommand("11988887777", null, null, FormaPagamento.Fiado, 0m,
                [new ItemVendaInput(produto.Id, 2)], ValorEntrega: 10m),
            CancellationToken.None);

        var clienteAtualizado = await db.Clientes.FindAsync(cliente.Id);
        Assert.Equal(210m, clienteAtualizado!.SaldoDevedor); // 200 + 10 de entrega
    }

    [Fact]
    public async Task Handle_ComValorEntrega_NaoFiado_DeveRegistrarPagamentoComValorTotalIncluindoEntrega()
    {
        var (db, produto) = await CriarProdutoAsync();
        var cliente = new Cliente("Maria Souza", "11988887777", "Rua A, 1");
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var handler = new RegistrarVendaCommandHandler(db);
        var result = await handler.Handle(
            new RegistrarVendaCommand("11988887777", null, null, FormaPagamento.Pix, 0m,
                [new ItemVendaInput(produto.Id, 1)], ValorEntrega: 5m),
            CancellationToken.None);

        var pagamento = Assert.Single(db.Pagamentos);
        Assert.Equal(105m, result.ValorTotal); // 100 + 5
        Assert.Equal(105m, pagamento.Valor);
    }

    [Fact]
    public async Task Handle_ComValorEntrega_NaoAlteraLucroTotal()
    {
        var (db, produto) = await CriarProdutoAsync();
        var handler = new RegistrarVendaCommandHandler(db);

        var result = await handler.Handle(
            new RegistrarVendaCommand("11988887777", "Maria Souza", "Rua A, 1", FormaPagamento.Dinheiro, 0m,
                [new ItemVendaInput(produto.Id, 1)], ValorEntrega: 20m),
            CancellationToken.None);

        // Lucro segue apenas itens (100 - 60 = 40); a entrega não compõe o lucro.
        Assert.Equal(40m, result.LucroTotal);
    }

    [Fact]
    public async Task Handle_DeveLancarExcecao_QuandoProdutoDesabilitado()
    {
        var (db, produto) = await CriarProdutoAsync();
        produto.Desativar();
        await db.SaveChangesAsync();

        var handler = new RegistrarVendaCommandHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.Handle(
                new RegistrarVendaCommand("11988887777", "Maria Souza", "Rua A, 1", FormaPagamento.Dinheiro, 0m,
                    [new ItemVendaInput(produto.Id, 1)]),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_DeveAgregarItensDoMesmoProdutoEmUmaLinhaNoHistorico()
    {
        var (db, produto) = await CriarProdutoAsync(estoque: 20);
        var handler = new RegistrarVendaCommandHandler(db);

        await handler.Handle(
            new RegistrarVendaCommand("11988887777", "Maria Souza", "Rua A, 1", FormaPagamento.Dinheiro, 0m,
                [new ItemVendaInput(produto.Id, 2), new ItemVendaInput(produto.Id, 1)]),
            CancellationToken.None);

        // 1 linha consolidada no histórico (soma 3), mas a MovimentacaoEstoque mantém 2 linhas (por item).
        var historico = Assert.Single(db.HistoricosEstoque);
        Assert.Equal(TipoHistoricoEstoque.Venda, historico.Tipo);
        Assert.Equal(3, historico.Quantidade);
        Assert.Equal("Venda de 3 produtos", historico.Descricao);
        Assert.Equal(2, db.MovimentacoesEstoque.Count());
    }

    [Fact]
    public async Task Handle_DeveCriarUmaLinhaPorProduto_QuandoProdutosDiferentes()
    {
        var (db, produto1) = await CriarProdutoAsync(estoque: 20);
        var produto2 = new Produto("Água 20L", TipoProduto.Agua, 20m, 10m, 15m);
        produto2.RegistrarEntrada(20);
        db.Produtos.Add(produto2);
        await db.SaveChangesAsync();

        var handler = new RegistrarVendaCommandHandler(db);

        await handler.Handle(
            new RegistrarVendaCommand("11988887777", "Maria Souza", "Rua A, 1", FormaPagamento.Dinheiro, 0m,
                [new ItemVendaInput(produto1.Id, 2), new ItemVendaInput(produto2.Id, 5)]),
            CancellationToken.None);

        var historicos = db.HistoricosEstoque.ToList();
        Assert.Equal(2, historicos.Count);
        Assert.Equal(2, historicos.Single(h => h.ProdutoId == produto1.Id).Quantidade);
        Assert.Equal(5, historicos.Single(h => h.ProdutoId == produto2.Id).Quantidade);
    }

    [Fact]
    public async Task Handle_DeveUsarSingular_QuandoVendeUmProduto()
    {
        var (db, produto) = await CriarProdutoAsync(estoque: 20);
        var handler = new RegistrarVendaCommandHandler(db);

        await handler.Handle(
            new RegistrarVendaCommand("11988887777", "Maria Souza", "Rua A, 1", FormaPagamento.Dinheiro, 0m,
                [new ItemVendaInput(produto.Id, 1)]),
            CancellationToken.None);

        var historico = Assert.Single(db.HistoricosEstoque);
        Assert.Equal("Venda de 1 produto", historico.Descricao);
    }

    [Fact]
    public async Task Handle_DeveRegistrarVendedor_QuandoAutenticado()
    {
        var (db, produto) = await CriarProdutoAsync(estoque: 20);
        var user = new ApplicationUser { Email = "maria@teste.com", UserName = "maria@teste.com" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var handler = new RegistrarVendaCommandHandler(db, new FakeAuthenticationStateProvider("maria@teste.com"));

        var result = await handler.Handle(
            new RegistrarVendaCommand("11988887777", "Maria Souza", "Rua A, 1", FormaPagamento.Dinheiro, 0m,
                [new ItemVendaInput(produto.Id, 1)]),
            CancellationToken.None);

        var venda = await db.Vendas.FindAsync(result.VendaId);
        Assert.Equal(user.Id, venda!.VendedorId);
        Assert.Equal("maria@teste.com", db.HistoricosEstoque.Single().Usuario);
    }

    [Fact]
    public async Task Handle_DeveDeixarVendedorNulo_QuandoDeslogado()
    {
        var (db, produto) = await CriarProdutoAsync(estoque: 20);
        var handler = new RegistrarVendaCommandHandler(db);

        var result = await handler.Handle(
            new RegistrarVendaCommand("11988887777", "Maria Souza", "Rua A, 1", FormaPagamento.Dinheiro, 0m,
                [new ItemVendaInput(produto.Id, 1)]),
            CancellationToken.None);

        var venda = await db.Vendas.FindAsync(result.VendaId);
        Assert.Null(venda!.VendedorId);
        Assert.Equal("sistema", db.HistoricosEstoque.Single().Usuario);
    }
}
