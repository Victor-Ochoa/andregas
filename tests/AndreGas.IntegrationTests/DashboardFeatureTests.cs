using AndreGas.Domain.Enums;
using AndreGas.Web.Features.Estoque.Cadastrar;
using AndreGas.Web.Features.Estoque.Movimentar;
using AndreGas.Web.Features.Home;
using AndreGas.Web.Features.Vendas.NovaVenda;

namespace AndreGas.IntegrationTests.Tests;

/// <summary>
/// Testes de integração das agregações do dashboard contra um Postgres real (via
/// <see cref="DatabaseFixture"/>), cobrindo a regra de que venda fiado conta como venda,
/// mas o lucro só é reconhecido quando o saldo é pago.
/// </summary>
public class DashboardFeatureTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ObterDashboard_VendaFiado_DeveContarComoVendaMasExcluirLucroDosLucrosDoDiaEMes()
    {
        using var db = fixture.CreateDbContext();

        var produtoId = await new CadastrarProdutoCommandHandler(db)
            .Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);
        await new RegistrarMovimentacaoEstoqueCommandHandler(db)
            .Handle(new RegistrarMovimentacaoEstoqueCommand(produtoId, TipoMovimentacaoEstoque.Entrada, 20, "Estoque inicial"), CancellationToken.None);

        var vendaHandler = new RegistrarVendaCommandHandler(db);

        // Venda paga hoje: entra no lucro (2 x 100, custo 60 => lucro 80).
        await vendaHandler.Handle(
            new RegistrarVendaCommand("11988887777", "Maria Souza", "Rua A, 1", FormaPagamento.Dinheiro, 0m,
                [new ItemVendaInput(produtoId, 2)]),
            CancellationToken.None);

        // Venda fiado hoje: conta como venda, mas o lucro (40) não entra no lucro do dia.
        await vendaHandler.Handle(
            new RegistrarVendaCommand("11999998888", "João Silva", "Rua B, 2", FormaPagamento.Fiado, 0m,
                [new ItemVendaInput(produtoId, 1)]),
            CancellationToken.None);

        var handler = new ObterDashboardQueryHandler(db, TimeProvider.System);
        var result = await handler.Handle(new ObterDashboardQuery(PeriodoDashboard.Hoje), CancellationToken.None);

        // As duas vendas contam nos totais (a fiada também é contabilizada como venda).
        Assert.Equal(300m, result.VendasTotal);
        Assert.Equal(2, result.VendasQuantidade);

        // Porém o lucro da venda fiado (40) é excluído — só o da venda paga (80) entra.
        Assert.Equal(80m, result.LucroTotal);

        // O gráfico do período conta as duas vendas no total, mas o lucro exibido exclui o fiado.
        Assert.Single(result.VendasPorPeriodo);
        Assert.Equal(300m, result.VendasPorPeriodo[0].Total);
        Assert.Equal(80m, result.VendasPorPeriodo[0].Lucro);
    }
}
