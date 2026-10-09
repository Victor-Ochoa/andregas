using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.Web.Common;

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
            .Include(v => v.Cliente)
            // Vendas excluídas (soft delete) ficam no banco, mas não contam no dashboard.
            .Where(v => v.ExcluidaEm == null
                        && (query.Periodo == PeriodoDashboard.Tudo || v.DataHora >= inicio))
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

        var pagamentos = await db.Pagamentos
            .Include(p => p.Cliente)
            .Where(p => query.Periodo == PeriodoDashboard.Tudo || p.Data >= inicio)
            .OrderByDescending(p => p.Data)
            .ToListAsync(cancellationToken);

        var produtosEstoqueBaixo = await db.Produtos
            .Where(p => p.Ativo && p.QuantidadeEstoque <= p.EstoqueMinimo)
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
            ProdutosEstoqueBaixo: produtosEstoqueBaixo,
            Vendas: vendas
                .OrderByDescending(v => v.DataHora)
                .Select(v => new VendaDetalhe(
                    BrasilTimeZone.ParaBrasilia(v.DataHora),
                    v.Cliente?.Nome ?? string.Empty,
                    v.Cliente?.Telefone ?? string.Empty,
                    v.FormaPagamento,
                    v.Status,
                    v.Itens.Sum(i => i.Quantidade),
                    v.ValorTotal,
                    v.LucroTotal,
                    v.Desconto,
                    v.ValorEntrega))
                .ToList(),
            Pagamentos: pagamentos
                .Select(p => new PagamentoDetalhe(
                    BrasilTimeZone.ParaBrasilia(p.Data),
                    p.Cliente?.Nome ?? string.Empty,
                    p.Cliente?.Telefone ?? string.Empty,
                    p.FormaPagamento,
                    p.Valor,
                    p.Observacao))
                .ToList());
    }

    private static DateTime ObterInicioPeriodo(PeriodoDashboard periodo, DateTime agora)
    {
        // "agora" é UTC; convertemos para o fuso de Brasília apenas para delimitar o calendário
        // ("hoje"/"este mês"). Janelas de duração (UltimaHora, Ultimas3Horas, Ultimas24Horas) são
        // físicas (rolam com o tempo) — o fuso não as altera.
        var hojeLocal = BrasilTimeZone.ParaBrasilia(agora).Date;

        static DateTime InicioLocal(DateTime local)
        {
            // Converte uma meia-noite local de volta para UTC para comparar com as colunas timestamptz.
            return BrasilTimeZone.ParaUtc(local);
        }

        return periodo switch
        {
            PeriodoDashboard.UltimaHora => agora.AddHours(-1),
            PeriodoDashboard.Ultimas3Horas => agora.AddHours(-3),
            PeriodoDashboard.Hoje => InicioLocal(hojeLocal),
            PeriodoDashboard.Ultimas24Horas => agora.AddHours(-24),
            PeriodoDashboard.Ultimos7Dias => InicioLocal(hojeLocal.AddDays(-6)),
            PeriodoDashboard.EsteMes => InicioLocal(new DateTime(hojeLocal.Year, hojeLocal.Month, 1)),
            PeriodoDashboard.Tudo => DateTime.MinValue,
            _ => InicioLocal(hojeLocal),
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
        static decimal LucroReconhecido(IEnumerable<Venda> vs) =>
            vs
                .Where(v => v.FormaPagamento != FormaPagamento.Fiado || v.Status == VendaStatus.FiadoQuitado)
                .Sum(v => v.LucroTotal);

        if (porHora)
        {
            return vendas
                .GroupBy(v => BrasilTimeZone.ParaBrasilia(v.DataHora).Hour)
                .OrderBy(g => g.Key)
                .Select(g => new VendaPeriodo($"{g.Key:D2}h", g.Sum(v => v.ValorTotal), LucroReconhecido(g)))
                .ToList();
        }

        return vendas
            .GroupBy(v => DateOnly.FromDateTime(BrasilTimeZone.ParaBrasilia(v.DataHora)))
            .OrderBy(g => g.Key)
            .Select(g => new VendaPeriodo($"{g.Key:dd/MM}", g.Sum(v => v.ValorTotal), LucroReconhecido(g)))
            .ToList();
    }
}