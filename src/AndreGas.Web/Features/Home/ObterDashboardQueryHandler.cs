using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Home;

public sealed class ObterDashboardQueryHandler(AppDbContext db, TimeProvider timeProvider) : IQueryHandler<ObterDashboardQuery, DashboardResult>
{
    public async ValueTask<DashboardResult> Handle(ObterDashboardQuery query, CancellationToken cancellationToken)
    {
        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var inicio = ObterInicioPeriodo(query.Periodo, agora);

        // ValorTotal/LucroTotal/Desconto são calculados em memória a partir dos Itens (não são
        // colunas mapeadas), então é preciso carregar as vendas com os itens antes de agregar.
        var vendas = await db.Vendas
            .Include(v => v.Itens)
            .Where(v => query.Periodo == PeriodoDashboard.Tudo || v.DataHora >= inicio)
            .ToListAsync(cancellationToken);

        // Venda fiado é contabilizada como venda, mas seu lucro só é reconhecido quando o
        // saldo devedor é pago. Por isso o lucro de vendas fiado ainda em aberto é excluído dos
        // lucros; vendas fiado já quitadas (Status == FiadoQuitado) entram no lucro (o total de
        // vendas sempre conta com o fiado).
        var lucroReconhecido = (IEnumerable<Venda> vendas) =>
            vendas
                .Where(v => v.FormaPagamento != FormaPagamento.Fiado || v.Status == VendaStatus.FiadoQuitado)
                .Sum(v => v.LucroTotal);

        var totalDevedor = await db.Clientes.SumAsync(c => c.SaldoDevedor, cancellationToken);

        var produtosEstoqueBaixo = await db.Produtos
            .Where(p => p.QuantidadeEstoque <= p.EstoqueMinimo)
            .OrderBy(p => p.Nome)
            .Select(p => new ProdutoEstoqueBaixoItem(p.Id, p.Nome, p.QuantidadeEstoque, p.EstoqueMinimo))
            .ToListAsync(cancellationToken);

        return new DashboardResult(
            Periodo: query.Periodo,
            VendasTotal: vendas.Sum(v => v.ValorTotal),
            VendasQuantidade: vendas.Count,
            LucroTotal: lucroReconhecido(vendas),
            DescontoTotal: vendas.Sum(v => v.Desconto),
            TotalDevedor: totalDevedor,
            VendasPorPeriodo: AgruparPorPeriodo(vendas, query.Periodo),
            ProdutosEstoqueBaixo: produtosEstoqueBaixo);
    }

    private static DateTime ObterInicioPeriodo(PeriodoDashboard periodo, DateTime agora)
    {
        var hoje = agora.Date;
        return periodo switch
        {
            PeriodoDashboard.UltimaHora => agora.AddHours(-1),
            PeriodoDashboard.Ultimas3Horas => agora.AddHours(-3),
            PeriodoDashboard.Hoje => hoje,
            PeriodoDashboard.Ultimas24Horas => agora.AddHours(-24),
            PeriodoDashboard.Ultimos7Dias => hoje.AddDays(-6),
            PeriodoDashboard.EsteMes => new DateTime(hoje.Year, hoje.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodoDashboard.Tudo => DateTime.MinValue,
            _ => hoje,
        };
    }

    private static List<VendaPeriodo> AgruparPorPeriodo(
        IReadOnlyList<Venda> vendas,
        PeriodoDashboard periodo)
    {
        // Janelas de até 24h (incluindo hoje) agrupam por hora; períodos maiores agrupam por dia.
        var porHora = periodo is PeriodoDashboard.UltimaHora
            or PeriodoDashboard.Ultimas3Horas
            or PeriodoDashboard.Ultimas24Horas
            or PeriodoDashboard.Hoje;

        // O lucro de vendas fiado ainda em aberto não é reconhecido no gráfico.
        decimal LucroReconhecido(IEnumerable<Venda> vs) =>
            vs
                .Where(v => v.FormaPagamento != FormaPagamento.Fiado || v.Status == VendaStatus.FiadoQuitado)
                .Sum(v => v.LucroTotal);

        if (porHora)
        {
            return vendas
                .GroupBy(v => v.DataHora.Hour)
                .OrderBy(g => g.Key)
                .Select(g => new VendaPeriodo($"{g.Key:D2}h", g.Sum(v => v.ValorTotal), LucroReconhecido(g)))
                .ToList();
        }

        return vendas
            .GroupBy(v => DateOnly.FromDateTime(v.DataHora))
            .OrderBy(g => g.Key)
            .Select(g => new VendaPeriodo($"{g.Key:dd/MM}", g.Sum(v => v.ValorTotal), LucroReconhecido(g)))
            .ToList();
    }
}
