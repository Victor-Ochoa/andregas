using Microsoft.EntityFrameworkCore;

using RevendaApp.Domain.Entities;
using RevendaApp.Domain.Enums;
using RevendaApp.Infrastructure;
using RevendaApp.IntegrationTests.TestHelpers;
using RevendaApp.Web.Common;
using RevendaApp.Web.Features.Estoque.Cadastrar;
using RevendaApp.Web.Features.Home;
using RevendaApp.Web.Features.Vendas.NovaVenda;

namespace RevendaApp.IntegrationTests.Tests;

public class RecompraFeatureTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public async Task InitializeAsync() => await fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AvisoAparece_QuandoDataPrevistaVencida_ESomeAoRegistrarVenda()
    {
        using var db = fixture.CreateDbContext();

        // Cliente + produto com AvisarRecompra: true, DiasPadraoRecompra: 45, com estoque.
        var cliente = new Cliente("Maria Souza", "11911112222", "Rua A, 1");
        var produtoId = await new CadastrarProdutoCommandHandler(db, TenantIntegracao.Contexto()).Handle(
            new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5,
                ControlaVasilhame: true, AvisarRecompra: true, DiasPadraoRecompra: 45),
            CancellationToken.None);
        db.Clientes.Add(cliente);
        var produto = await db.Produtos.FindAsync([produtoId]);
        produto!.RegistrarEntrada(10);
        await db.SaveChangesAsync();

        // Venda 45 dias atrás (via Venda direta + recálculo, pois o comando não expõe DataHora).
        var vendaAntiga = new Venda(cliente.Id, FormaPagamento.Dinheiro, DateTime.UtcNow.AddDays(-45));
        vendaAntiga.AdicionarItem(produtoId, 1, 100m, 60m);
        db.Vendas.Add(vendaAntiga);
        await db.SaveChangesAsync();
        await RecorrenciaCompraAprendizado.RecalcularAsync(db, cliente.Id, produtoId, CancellationToken.None);
        await db.SaveChangesAsync();

        // Dashboard HOJE (TimeProvider real) → recompras contém Maria + produto.
        var dashboardHandler = new ObterDashboardQueryHandler(db, TenantIntegracao.Contexto(), TimeProvider.System);
        var result = await dashboardHandler.Handle(new ObterDashboardQuery(PeriodoDashboard.Tudo), CancellationToken.None);
        Assert.Contains(result.Recompras, r => r.ClienteId == cliente.Id && r.ProdutoId == produtoId);

        // Registrar nova venda HOJE via handler real (produto com estoque) → DataUltimaCompra vira hoje.
        await new RegistrarVendaCommandHandler(db, TenantIntegracao.Contexto()).Handle(
            new RegistrarVendaCommand(cliente.Id, null, null, null, FormaPagamento.Dinheiro, 0m,
                [new ItemVendaInput(produtoId, 1)]),
            CancellationToken.None);

        // Recorrência recalcula: agora última compra = hoje → dataPrevista futura → fora dos avisos.
        var result2 = await dashboardHandler.Handle(new ObterDashboardQuery(PeriodoDashboard.Tudo), CancellationToken.None);
        Assert.DoesNotContain(result2.Recompras, r => r.ClienteId == cliente.Id && r.ProdutoId == produtoId);
    }
}