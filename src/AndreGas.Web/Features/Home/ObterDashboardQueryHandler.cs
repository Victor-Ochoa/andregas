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

        var vendasPorDia = vendasDoMes
            .GroupBy(v => DateOnly.FromDateTime(v.DataHora))
            .OrderBy(g => g.Key)
            .Select(g => new VendaDiaria(g.Key, g.Sum(v => v.ValorTotal), g.Sum(v => v.LucroTotal)))
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
            LucroHoje: vendasHoje.Sum(v => v.LucroTotal),
            LucroMes: vendasDoMes.Sum(v => v.LucroTotal),
            VendasPorDiaNoMes: vendasPorDia,
            ProdutosEstoqueBaixo: produtosEstoqueBaixo);
    }
}
