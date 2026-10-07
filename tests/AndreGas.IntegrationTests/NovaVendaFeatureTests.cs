using AndreGas.Domain.Enums;
using AndreGas.Web.Features.Clientes.Listar;
using AndreGas.Web.Features.Estoque.Cadastrar;
using AndreGas.Web.Features.Estoque.Listar;
using AndreGas.Web.Features.Vendas.NovaVenda;

namespace AndreGas.IntegrationTests.Tests;

/// <summary>
/// Testes de integração do fluxo completo de Nova Venda contra um Postgres real (via
/// <see cref="DatabaseFixture"/>): cadastro automático de cliente pelo telefone, baixa de
/// estoque e atualização de saldo devedor.
/// </summary>
public class NovaVendaFeatureTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RegistrarVenda_ComClienteNovo_DeveCadastrarClienteBaixarEstoqueENaoAlterarSaldo()
    {
        using var db = fixture.CreateDbContext();
        var produtoId = await new CadastrarProdutoCommandHandler(db)
            .Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);
        await new AndreGas.Web.Features.Estoque.Movimentar.RegistrarMovimentacaoEstoqueCommandHandler(db)
            .Handle(new AndreGas.Web.Features.Estoque.Movimentar.RegistrarMovimentacaoEstoqueCommand(produtoId, TipoMovimentacaoEstoque.Entrada, 20, "Estoque inicial"), CancellationToken.None);

        var vendaHandler = new RegistrarVendaCommandHandler(db);
        var result = await vendaHandler.Handle(
            new RegistrarVendaCommand("11988887777", "Maria Souza", "Rua A, 1", FormaPagamento.Dinheiro, 0m,
                [new ItemVendaInput(produtoId, 3)]),
            CancellationToken.None);

        Assert.Equal(300m, result.ValorTotal);
        Assert.Equal(120m, result.LucroTotal);

        var clientes = await new ListarClientesQueryHandler(db).Handle(new ListarClientesQuery(), CancellationToken.None);
        var cliente = Assert.Single(clientes);
        Assert.Equal("Maria Souza", cliente.Nome);
        Assert.Equal(0m, cliente.SaldoDevedor);

        var produtos = await new ListarProdutosQueryHandler(db).Handle(new ListarProdutosQuery(), CancellationToken.None);
        var produto = Assert.Single(produtos);
        Assert.Equal(17, produto.QuantidadeEstoque); // 20 - 3
    }

    [Fact]
    public async Task RegistrarVenda_Fiado_DeveSomarValorTotalAoSaldoDevedorDoCliente()
    {
        using var db = fixture.CreateDbContext();
        var produtoId = await new CadastrarProdutoCommandHandler(db)
            .Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);
        await new AndreGas.Web.Features.Estoque.Movimentar.RegistrarMovimentacaoEstoqueCommandHandler(db)
            .Handle(new AndreGas.Web.Features.Estoque.Movimentar.RegistrarMovimentacaoEstoqueCommand(produtoId, TipoMovimentacaoEstoque.Entrada, 20, "Estoque inicial"), CancellationToken.None);

        var vendaHandler = new RegistrarVendaCommandHandler(db);
        var result = await vendaHandler.Handle(
            new RegistrarVendaCommand("11999998888", "João Silva", "Rua B, 2", FormaPagamento.Fiado, 10m,
                [new ItemVendaInput(produtoId, 2)]),
            CancellationToken.None);

        Assert.Equal(190m, result.ValorTotal); // 200 - 10

        var clientes = await new ListarClientesQueryHandler(db).Handle(new ListarClientesQuery(), CancellationToken.None);
        var cliente = Assert.Single(clientes);
        Assert.Equal(190m, cliente.SaldoDevedor);

        // Venda fiado não gera pagamento no ato.
        Assert.Empty(db.Pagamentos);
    }

    [Fact]
    public async Task RegistrarVenda_NaoFiado_DeveRegistrarPagamentoSemAlterarSaldo()
    {
        using var db = fixture.CreateDbContext();
        var produtoId = await new CadastrarProdutoCommandHandler(db)
            .Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);
        await new AndreGas.Web.Features.Estoque.Movimentar.RegistrarMovimentacaoEstoqueCommandHandler(db)
            .Handle(new AndreGas.Web.Features.Estoque.Movimentar.RegistrarMovimentacaoEstoqueCommand(produtoId, TipoMovimentacaoEstoque.Entrada, 20, "Estoque inicial"), CancellationToken.None);

        var vendaHandler = new RegistrarVendaCommandHandler(db);
        var result = await vendaHandler.Handle(
            new RegistrarVendaCommand("11977776666", "Ana Lima", "Rua C, 3", FormaPagamento.Pix, 10m,
                [new ItemVendaInput(produtoId, 2)]),
            CancellationToken.None);

        Assert.Equal(190m, result.ValorTotal); // 200 - 10

        var clientes = await new ListarClientesQueryHandler(db).Handle(new ListarClientesQuery(), CancellationToken.None);
        var cliente = Assert.Single(clientes);
        Assert.Equal(0m, cliente.SaldoDevedor); // não é fiado

        var pagamento = Assert.Single(db.Pagamentos);
        Assert.Equal(cliente.Id, pagamento.ClienteId);
        Assert.Equal(190m, pagamento.Valor);
        Assert.Equal(FormaPagamento.Pix, pagamento.FormaPagamento);
    }

    [Fact]
    public async Task RegistrarVenda_FiadoComEntrega_DeveSomarEntregaAoSaldoDevedor()
    {
        using var db = fixture.CreateDbContext();
        var produtoId = await new CadastrarProdutoCommandHandler(db)
            .Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);
        await new AndreGas.Web.Features.Estoque.Movimentar.RegistrarMovimentacaoEstoqueCommandHandler(db)
            .Handle(new AndreGas.Web.Features.Estoque.Movimentar.RegistrarMovimentacaoEstoqueCommand(produtoId, TipoMovimentacaoEstoque.Entrada, 20, "Estoque inicial"), CancellationToken.None);

        var vendaHandler = new RegistrarVendaCommandHandler(db);
        var result = await vendaHandler.Handle(
            new RegistrarVendaCommand("11999998888", "João Silva", "Rua B, 2", FormaPagamento.Fiado, 0m,
                [new ItemVendaInput(produtoId, 2)], ValorEntrega: 10m),
            CancellationToken.None);

        Assert.Equal(210m, result.ValorTotal); // 200 + 10

        var clientes = await new ListarClientesQueryHandler(db).Handle(new ListarClientesQuery(), CancellationToken.None);
        var cliente = Assert.Single(clientes);
        Assert.Equal(210m, cliente.SaldoDevedor);

        // Fiado continua sem gerar pagamento no ato.
        Assert.Empty(db.Pagamentos);

        // A venda persiste o valor de entrega no banco.
        var venda = await db.Vendas.FindAsync(result.VendaId);
        Assert.Equal(10m, venda!.ValorEntrega);
    }

    [Fact]
    public async Task RegistrarVenda_NaoFiadoComEntrega_DeveRegistrarPagamentoIncluindoEntrega()
    {
        using var db = fixture.CreateDbContext();
        var produtoId = await new CadastrarProdutoCommandHandler(db)
            .Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);
        await new AndreGas.Web.Features.Estoque.Movimentar.RegistrarMovimentacaoEstoqueCommandHandler(db)
            .Handle(new AndreGas.Web.Features.Estoque.Movimentar.RegistrarMovimentacaoEstoqueCommand(produtoId, TipoMovimentacaoEstoque.Entrada, 20, "Estoque inicial"), CancellationToken.None);

        var vendaHandler = new RegistrarVendaCommandHandler(db);
        var result = await vendaHandler.Handle(
            new RegistrarVendaCommand("11977776666", "Ana Lima", "Rua C, 3", FormaPagamento.Pix, 0m,
                [new ItemVendaInput(produtoId, 1)], ValorEntrega: 5m),
            CancellationToken.None);

        Assert.Equal(105m, result.ValorTotal); // 100 + 5

        var clientes = await new ListarClientesQueryHandler(db).Handle(new ListarClientesQuery(), CancellationToken.None);
        var cliente = Assert.Single(clientes);
        Assert.Equal(0m, cliente.SaldoDevedor); // não é fiado

        var pagamento = Assert.Single(db.Pagamentos);
        Assert.Equal(cliente.Id, pagamento.ClienteId);
        Assert.Equal(105m, pagamento.Valor);
        Assert.Equal(FormaPagamento.Pix, pagamento.FormaPagamento);
    }
}
