using Microsoft.EntityFrameworkCore;

using RevendaApp.Domain.Entities;
using RevendaApp.Domain.Enums;
using RevendaApp.Infrastructure;
using RevendaApp.UnitTests.TestHelpers;
using RevendaApp.Web.Common;

namespace RevendaApp.UnitTests.Common;

public class RecorrenciaCompraAprendizadoTests
{
    private static async Task<(AppDbContext db, Guid clienteId, Guid produtoId)> CriarBaseAsync()
    {
        var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11999999999", "Rua A, 10");
        // AvisarRecompra: true, DiasPadraoRecompra: 45
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5,
            avisarRecompra: true, diasPadraoRecompra: 45);
        db.Clientes.Add(cliente);
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();
        return (db, cliente.Id, produto.Id);
    }

    private static async Task<Venda> CriarVendaAsync(AppDbContext db, Guid clienteId, Guid produtoId, DateTime data, int quantidade = 1)
    {
        var venda = new Venda(clienteId, FormaPagamento.Dinheiro, data);
        venda.AdicionarItem(produtoId, quantidade, 100m, 60m);
        db.Vendas.Add(venda);
        return venda;
    }

    [Fact]
    public async Task Recalcular_UmaVenda_UsaDiasPadraoRecompra()
    {
        var (db, clienteId, produtoId) = await CriarBaseAsync();
        await CriarVendaAsync(db, clienteId, produtoId, new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        await db.SaveChangesAsync();

        await RecorrenciaCompraAprendizado.RecalcularAsync(db, clienteId, produtoId, CancellationToken.None);
        await db.SaveChangesAsync();

        var rec = await db.RecorrenciasCompra.SingleAsync();
        Assert.Equal(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), rec.DataUltimaCompra);
        Assert.Equal(45m, rec.IntervaloMedioDias);
        Assert.Equal(1, rec.QuantidadeCompras);
    }

    [Fact]
    public async Task Recalcular_DuasVendas_ComGap45_CalculaMedia()
    {
        var (db, clienteId, produtoId) = await CriarBaseAsync();
        await CriarVendaAsync(db, clienteId, produtoId, new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        await CriarVendaAsync(db, clienteId, produtoId, new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc)); // gap 45
        await db.SaveChangesAsync();

        await RecorrenciaCompraAprendizado.RecalcularAsync(db, clienteId, produtoId, CancellationToken.None);
        await db.SaveChangesAsync();

        var rec = await db.RecorrenciasCompra.SingleAsync();
        Assert.Equal(45m, rec.IntervaloMedioDias);
        Assert.Equal(2, rec.QuantidadeCompras);
        Assert.Equal(new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc), rec.DataUltimaCompra);
    }

    [Fact]
    public async Task Recalcular_TresVendas_Gaps45E30_CalculaMedia37_5()
    {
        var (db, clienteId, produtoId) = await CriarBaseAsync();
        await CriarVendaAsync(db, clienteId, produtoId, new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        await CriarVendaAsync(db, clienteId, produtoId, new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc)); // gap 45
        await CriarVendaAsync(db, clienteId, produtoId, new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc)); // gap 30
        await db.SaveChangesAsync();

        await RecorrenciaCompraAprendizado.RecalcularAsync(db, clienteId, produtoId, CancellationToken.None);
        await db.SaveChangesAsync();

        var rec = await db.RecorrenciasCompra.SingleAsync();
        Assert.Equal(37.5m, rec.IntervaloMedioDias);
        Assert.Equal(3, rec.QuantidadeCompras);
    }

    [Fact]
    public async Task Recalcular_GapMenorQue7Dias_Ignorado_UsaDefaultDoProduto()
    {
        var (db, clienteId, produtoId) = await CriarBaseAsync();
        await CriarVendaAsync(db, clienteId, produtoId, new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        await CriarVendaAsync(db, clienteId, produtoId, new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc)); // gap 1 < 7
        await db.SaveChangesAsync();

        await RecorrenciaCompraAprendizado.RecalcularAsync(db, clienteId, produtoId, CancellationToken.None);
        await db.SaveChangesAsync();

        var rec = await db.RecorrenciasCompra.SingleAsync();
        Assert.Equal(45m, rec.IntervaloMedioDias); // default, não gap de 1 dia
        Assert.Equal(2, rec.QuantidadeCompras);
    }

    [Fact]
    public async Task Recalcular_SemVendas_RemoveRecorrencia()
    {
        var (db, clienteId, produtoId) = await CriarBaseAsync();
        await CriarVendaAsync(db, clienteId, produtoId, new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        await db.SaveChangesAsync();
        await RecorrenciaCompraAprendizado.RecalcularAsync(db, clienteId, produtoId, CancellationToken.None);
        await db.SaveChangesAsync(); // persiste a recorrência (como faz o handler após o recálculo)

        // Remove todas as vendas (simula exclusão) e recalcula.
        db.Vendas.RemoveRange(db.Vendas);
        await db.SaveChangesAsync();
        await RecorrenciaCompraAprendizado.RecalcularAsync(db, clienteId, produtoId, CancellationToken.None);
        await db.SaveChangesAsync(); // persiste a remoção da recorrência (como faz o handler)

        Assert.Empty(await db.RecorrenciasCompra.ToListAsync());
    }

    [Fact]
    public async Task Recalcular_VendaExcluida_NaoConta()
    {
        var (db, clienteId, produtoId) = await CriarBaseAsync();
        var v1 = await CriarVendaAsync(db, clienteId, produtoId, new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        var v2 = await CriarVendaAsync(db, clienteId, produtoId, new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc));
        v2.Excluir();
        await db.SaveChangesAsync();

        await RecorrenciaCompraAprendizado.RecalcularAsync(db, clienteId, produtoId, CancellationToken.None);
        await db.SaveChangesAsync();

        var rec = await db.RecorrenciasCompra.SingleAsync();
        Assert.Equal(45m, rec.IntervaloMedioDias); // só 1 venda vigente → default
        Assert.Equal(1, rec.QuantidadeCompras);
    }
}