using AndreGas.Domain.Enums;
using AndreGas.Web.Features.Clientes.Listar;
using AndreGas.Web.Features.Clientes.Pagar;
using AndreGas.Web.Features.Estoque.Cadastrar;
using AndreGas.Web.Features.Estoque.Movimentar;
using AndreGas.Web.Features.Home;
using AndreGas.Web.Features.Vendas.NovaVenda;

namespace AndreGas.IntegrationTests.Tests;

/// <summary>
/// Testes de integração do fluxo de pagamento de saldo devedor (fiado) contra Postgres real
/// (via <see cref="DatabaseFixture"/>): registrar venda fiado, pagar o débito, verificar saldo
/// zerado, venda quitada e lucro reconhecido no dashboard.
/// </summary>
public class PagamentoFeatureTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PagarSaldoDevedor_DeveZerarSaldoQuitarVendaEReconhecerLucroNoDashboard()
    {
        using (var db = fixture.CreateDbContext())
        {
            var produtoId = await new CadastrarProdutoCommandHandler(db)
                .Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);
            await new RegistrarMovimentacaoEstoqueCommandHandler(db)
                .Handle(new RegistrarMovimentacaoEstoqueCommand(produtoId, TipoMovimentacaoEstoque.Entrada, 20, "Estoque inicial"), CancellationToken.None);

            // Venda fiado de 2 itens (200 total).
            var vendaResult = await new RegistrarVendaCommandHandler(db).Handle(
                new RegistrarVendaCommand(null, "João Silva", "11999998888", "Rua B, 2", FormaPagamento.Fiado, 0m,
                    [new ItemVendaInput(produtoId, 2)]),
                CancellationToken.None);

            Assert.Equal(200m, vendaResult.ValorTotal);

            // Antes do pagamento, o lucro não é reconhecido (fiado em aberto).
            var dashboardAntes = await new ObterDashboardQueryHandler(db, TimeProvider.System)
                .Handle(new ObterDashboardQuery(PeriodoDashboard.Hoje), CancellationToken.None);
            Assert.Equal(200m, dashboardAntes.VendasTotal);
            Assert.Equal(0m, dashboardAntes.LucroTotal);
        }

        // Novo contexto: registra o pagamento de 200 (total do fiado).
        using (var db = fixture.CreateDbContext())
        {
            var clientes = await new ListarClientesQueryHandler(db).Handle(new ListarClientesQuery(), CancellationToken.None);
            var cliente = Assert.Single(clientes);
            Assert.Equal(200m, cliente.SaldoDevedor);

            var pagamentoResult = await new RegistrarPagamentoCommandHandler(db, new AndreGas.Web.Common.VendasAtualizadasNotifier()).Handle(
                new RegistrarPagamentoCommand(cliente.Id, 200m, FormaPagamento.Dinheiro),
                CancellationToken.None);

            Assert.Equal(0m, pagamentoResult.SaldoDevedorRestante);
        }

        // Novo contexto: verifica saldo zerado, venda quitada e lucro reconhecido.
        using (var db = fixture.CreateDbContext())
        {
            var clientes = await new ListarClientesQueryHandler(db).Handle(new ListarClientesQuery(), CancellationToken.None);
            var cliente = Assert.Single(clientes);
            Assert.Equal(0m, cliente.SaldoDevedor);

            var dashboardDepois = await new ObterDashboardQueryHandler(db, TimeProvider.System)
                .Handle(new ObterDashboardQuery(PeriodoDashboard.Hoje), CancellationToken.None);
            Assert.Equal(200m, dashboardDepois.VendasTotal);
            // Lucro da venda fiado quitada (2 x 40 = 80m) agora é reconhecido.
            Assert.Equal(80m, dashboardDepois.LucroTotal);
        }
    }

    [Fact]
    public async Task PagarSaldoDevedor_ComEntrega_DeveQuitarSaldoIncluindoEntrega()
    {
        // Venda fiado com entrega: 200 (itens) + 10 (entrega) => saldo devedor 210, que o
        // FIFO de pagamento abate integralmente.
        using (var db = fixture.CreateDbContext())
        {
            var produtoId = await new CadastrarProdutoCommandHandler(db)
                .Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);
            await new RegistrarMovimentacaoEstoqueCommandHandler(db)
                .Handle(new RegistrarMovimentacaoEstoqueCommand(produtoId, TipoMovimentacaoEstoque.Entrada, 20, "Estoque inicial"), CancellationToken.None);

            var vendaResult = await new RegistrarVendaCommandHandler(db).Handle(
                new RegistrarVendaCommand(null, "João Silva", "11999998888", "Rua B, 2", FormaPagamento.Fiado, 0m,
                    [new ItemVendaInput(produtoId, 2)], ValorEntrega: 10m),
                CancellationToken.None);

            Assert.Equal(210m, vendaResult.ValorTotal); // 200 + 10

            // Antes do pagamento, o saldo inclui a entrega e o lucro não é reconhecido.
            var dashboardAntes = await new ObterDashboardQueryHandler(db, TimeProvider.System)
                .Handle(new ObterDashboardQuery(PeriodoDashboard.Hoje), CancellationToken.None);
            Assert.Equal(210m, dashboardAntes.VendasTotal);
            Assert.Equal(0m, dashboardAntes.LucroTotal);
        }

        // Registra pagamento do total com entrega (210) → zera saldo e quita a venda.
        using (var db = fixture.CreateDbContext())
        {
            var clientes = await new ListarClientesQueryHandler(db).Handle(new ListarClientesQuery(), CancellationToken.None);
            var cliente = Assert.Single(clientes);
            Assert.Equal(210m, cliente.SaldoDevedor);

            var pagamentoResult = await new RegistrarPagamentoCommandHandler(db, new AndreGas.Web.Common.VendasAtualizadasNotifier()).Handle(
                new RegistrarPagamentoCommand(cliente.Id, 210m, FormaPagamento.Dinheiro),
                CancellationToken.None);

            Assert.Equal(0m, pagamentoResult.SaldoDevedorRestante);
        }

        // Saldo zerado, venda quitada e lucro reconhecido (itens 2x40 = 80; a entrega não é lucro).
        using (var db = fixture.CreateDbContext())
        {
            var dashboardDepois = await new ObterDashboardQueryHandler(db, TimeProvider.System)
                .Handle(new ObterDashboardQuery(PeriodoDashboard.Hoje), CancellationToken.None);
            Assert.Equal(210m, dashboardDepois.VendasTotal);
            Assert.Equal(80m, dashboardDepois.LucroTotal);
        }
    }
}
