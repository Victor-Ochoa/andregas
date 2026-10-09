using AndreGas.Domain.Enums;
using AndreGas.Web.Features.Clientes.Listar;
using AndreGas.Web.Features.Estoque.Cadastrar;
using AndreGas.Web.Features.Estoque.Editar;
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
            new RegistrarVendaCommand(null, "Maria Souza", "11988887777", "Rua A, 1", FormaPagamento.Dinheiro, 0m,
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
            new RegistrarVendaCommand(null, "João Silva", "11999998888", "Rua B, 2", FormaPagamento.Fiado, 10m,
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
            new RegistrarVendaCommand(null, "Ana Lima", "11977776666", "Rua C, 3", FormaPagamento.Pix, 10m,
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
            new RegistrarVendaCommand(null, "João Silva", "11999998888", "Rua B, 2", FormaPagamento.Fiado, 0m,
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
            new RegistrarVendaCommand(null, "Ana Lima", "11977776666", "Rua C, 3", FormaPagamento.Pix, 0m,
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

    [Fact]
    public async Task RegistrarVenda_DeveSerRejeitadaPeloValidador_QuandoProdutoDesabilitado()
    {
        using var db = fixture.CreateDbContext();
        var produtoId = await new CadastrarProdutoCommandHandler(db)
            .Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);
        await new AndreGas.Web.Features.Estoque.Movimentar.RegistrarMovimentacaoEstoqueCommandHandler(db)
            .Handle(new AndreGas.Web.Features.Estoque.Movimentar.RegistrarMovimentacaoEstoqueCommand(produtoId, TipoMovimentacaoEstoque.Entrada, 20, "Estoque inicial"), CancellationToken.None);

        // Desabilita o produto via edição (estoque suficiente, mas inativo).
        await new AtualizarProdutoCommandHandler(db).Handle(new AtualizarProdutoCommand(
            produtoId, "Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5, Ativo: false), CancellationToken.None);

        var validator = new RegistrarVendaCommandValidator(db);
        var result = await validator.ValidateAsync(
            new RegistrarVendaCommand(null, "Maria Souza", "11988887777", "Rua A, 1", FormaPagamento.Dinheiro, 0m,
                [new ItemVendaInput(produtoId, 1)]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Itens[0]");
    }

    [Fact]
    public async Task RegistrarVenda_ComPrecoPersonalizado_CriaAcordo_EDisponibilizaParaProximaVenda()
    {
        using var db = fixture.CreateDbContext();
        var produtoId = await new CadastrarProdutoCommandHandler(db)
            .Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);
        await new AndreGas.Web.Features.Estoque.Movimentar.RegistrarMovimentacaoEstoqueCommandHandler(db)
            .Handle(new AndreGas.Web.Features.Estoque.Movimentar.RegistrarMovimentacaoEstoqueCommand(produtoId, TipoMovimentacaoEstoque.Entrada, 20, "Estoque inicial"), CancellationToken.None);

        var vendaHandler = new RegistrarVendaCommandHandler(db);

        // 1ª venda para o Restaurante A: gás a 90 (acordo negociado).
        var result1 = await vendaHandler.Handle(
            new RegistrarVendaCommand(null, "Restaurante A", "11988887777", "Rua A, 1", FormaPagamento.Dinheiro, 0m,
                [new ItemVendaInput(produtoId, 2, PrecoUnitario: 90m)]),
            CancellationToken.None);

        Assert.Equal(180m, result1.ValorTotal); // 90 * 2
        var acordo = Assert.Single(db.PrecosPersonalizadosClientes);
        Assert.Equal(90m, acordo.Preco);

        // A consulta de preços do cliente passa a retornar o acordo, então a próxima tela de
        // nova venda pré-preenche 90 em vez do preço padrão (100).
        var precos = await new ObterPrecosPersonalizadosQueryHandler(db)
            .Handle(new ObterPrecosPersonalizadosQuery(result1.ClienteId), CancellationToken.None);
        Assert.Equal(90m, precos[produtoId]);

        // 2ª venda para o mesmo cliente, com o preço que a UI enviaria (pre-preenchido pelo acordo).
        var result2 = await vendaHandler.Handle(
            new RegistrarVendaCommand(result1.ClienteId, null, null, null, FormaPagamento.Dinheiro, 0m,
                [new ItemVendaInput(produtoId, 1, PrecoUnitario: 90m)]),
            CancellationToken.None);

        Assert.Equal(90m, result2.ValorTotal); // acordo reutilizado
        Assert.Single(db.PrecosPersonalizadosClientes); // segue um único acordo (atualizado, não duplicado)
        var item = Assert.Single(db.ItensVenda.Where(i => i.VendaId == result2.VendaId));
        Assert.Equal(90m, item.PrecoUnitario);
    }

    [Fact]
    public async Task RegistrarVenda_PrecoNoPadrao_DeveRemoverAcordoExistente()
    {
        using var db = fixture.CreateDbContext();
        var produtoId = await new CadastrarProdutoCommandHandler(db)
            .Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);
        await new AndreGas.Web.Features.Estoque.Movimentar.RegistrarMovimentacaoEstoqueCommandHandler(db)
            .Handle(new AndreGas.Web.Features.Estoque.Movimentar.RegistrarMovimentacaoEstoqueCommand(produtoId, TipoMovimentacaoEstoque.Entrada, 20, "Estoque inicial"), CancellationToken.None);

        var vendaHandler = new RegistrarVendaCommandHandler(db);

        // Cliente existente com acordo de 90.
        // (Primeiro criamos o cliente e o acordo via uma venda com preço personalizado.)
        var vendaCreate = await vendaHandler.Handle(
            new RegistrarVendaCommand(null, "Restaurante A", "11988887777", "Rua A, 1", FormaPagamento.Dinheiro, 0m,
                [new ItemVendaInput(produtoId, 1, PrecoUnitario: 90m)]),
            CancellationToken.None);
        Assert.Single(db.PrecosPersonalizadosClientes);

        // 2ª venda com preço no padrão (100) -> remove o acordo.
        var result2 = await vendaHandler.Handle(
            new RegistrarVendaCommand(vendaCreate.ClienteId, null, null, null, FormaPagamento.Dinheiro, 0m,
                [new ItemVendaInput(produtoId, 1, PrecoUnitario: 100m)]),
            CancellationToken.None);

        Assert.Equal(100m, result2.ValorTotal);
        Assert.Empty(db.PrecosPersonalizadosClientes);
    }

    [Fact]
    public async Task RegistrarVenda_GasDoPovo_ComPrecoInformado_DeveUsarPrecoSubsidiadoENaoCriarAcordo()
    {
        using var db = fixture.CreateDbContext();
        var produtoId = await new CadastrarProdutoCommandHandler(db)
            .Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);
        await new AndreGas.Web.Features.Estoque.Movimentar.RegistrarMovimentacaoEstoqueCommandHandler(db)
            .Handle(new AndreGas.Web.Features.Estoque.Movimentar.RegistrarMovimentacaoEstoqueCommand(produtoId, TipoMovimentacaoEstoque.Entrada, 20, "Estoque inicial"), CancellationToken.None);

        var vendaHandler = new RegistrarVendaCommandHandler(db);
        var result = await vendaHandler.Handle(
            new RegistrarVendaCommand(null, "Restaurante A", "11988887777", "Rua A, 1", FormaPagamento.GasDoPovo, 0m,
                [new ItemVendaInput(produtoId, 1, PrecoUnitario: 90m)]),
            CancellationToken.None);

        Assert.Equal(80m, result.ValorTotal); // PrecoGasDoPovo, não o 90 informado
        Assert.Empty(db.PrecosPersonalizadosClientes);
    }

    [Fact]
    public async Task RegistrarVenda_DeveConsolidarHistoricoDeEstoquePorProduto_EVendedorNuloSemAuth()
    {
        using var db = fixture.CreateDbContext();
        var produtoId = await new CadastrarProdutoCommandHandler(db)
            .Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);
        await new AndreGas.Web.Features.Estoque.Movimentar.RegistrarMovimentacaoEstoqueCommandHandler(db)
            .Handle(new AndreGas.Web.Features.Estoque.Movimentar.RegistrarMovimentacaoEstoqueCommand(produtoId, TipoMovimentacaoEstoque.Entrada, 20, "Estoque inicial"), CancellationToken.None);

        var vendaHandler = new RegistrarVendaCommandHandler(db);
        var result = await vendaHandler.Handle(
            new RegistrarVendaCommand(null, "Maria Souza", "11988887777", "Rua A, 1", FormaPagamento.Dinheiro, 0m,
                [new ItemVendaInput(produtoId, 2), new ItemVendaInput(produtoId, 1)]),
            CancellationToken.None);

        // 1 histórico consolidado do tipo Venda (soma 3) com vínculo à venda; o cadastro e a entrada
        // também geraram históricos, mas o de venda é único e consolidado.
        var historico = Assert.Single(db.HistoricosEstoque.Where(h => h.Tipo == TipoHistoricoEstoque.Venda));
        Assert.Equal(3, historico.Quantidade);
        Assert.Equal("Venda de 3 produtos", historico.Descricao);
        Assert.Equal(result.VendaId, historico.VendaId);
        Assert.Equal(2, db.MovimentacoesEstoque.Count(m => m.Tipo == TipoMovimentacaoEstoque.Saida));

        // Sem usuário autenticado, o vendedor fica nulo.
        var venda = await db.Vendas.FindAsync(result.VendaId);
        Assert.Null(venda!.VendedorId);
    }
}