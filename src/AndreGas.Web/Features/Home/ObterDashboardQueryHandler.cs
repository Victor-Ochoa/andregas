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
        var hoje = agora.Date;
        var inicioMes = new DateTime(agora.Year, agora.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // ValorTotal/LucroTotal são calculados em memória a partir dos Itens (não são colunas
        // mapeadas), então é preciso carregar as vendas com os itens antes de agregar.
        var vendasDoMes = await db.Vendas
            .Where(v => v.DataHora >= inicioMes)
            .Include(v => v.Itens)
            .ToListAsync(cancellationToken);

        var vendasHoje = vendasDoMes.Where(v => v.DataHora.Date == hoje).ToList();

        // Venda fiado é contabilizada como venda, mas seu lucro só é reconhecido quando o
        // saldo devedor é pago. Por isso o lucro de vendas fiado é excluído dos lucros de hoje,
        // do mês e do gráfico diário (o total de vendas continua contando com o fiado).
        var lucroReconhecido = (IEnumerable<Venda> vendas) =>
            vendas.Where(v => v.FormaPagamento != FormaPagamento.Fiado).Sum(v => v.LucroTotal);

        var vendasPorDia = vendasDoMes
            .GroupBy(v => DateOnly.FromDateTime(v.DataHora))
            .OrderBy(g => g.Key)
            .Select(g => new VendaDiaria(
                g.Key,
                g.Sum(v => v.ValorTotal),
                lucroReconhecido(g)))
            .ToList();

        var produtosEstoqueBaixo = await db.Produtos
            .Where(p => p.QuantidadeEstoque <= p.EstoqueMinimo)
            .OrderBy(p => p.Nome)
            .Select(p => new ProdutoEstoqueBaixoItem(p.Id, p.Nome, p.QuantidadeEstoque, p.EstoqueMinimo))
            .ToListAsync(cancellationToken);

        return new DashboardResult(
            VendasHojeTotal: vendasHoje.Sum(v => v.ValorTotal),
            VendasHojeQuantidade: vendasHoje.Count,
            VendasMesTotal: vendasDoMes.Sum(v => v.ValorTotal),
            VendasMesQuantidade: vendasDoMes.Count,
            LucroHoje: lucroReconhecido(vendasHoje),
            LucroMes: lucroReconhecido(vendasDoMes),
            VendasPorDiaNoMes: vendasPorDia,
            ProdutosEstoqueBaixo: produtosEstoqueBaixo);
    }
}
