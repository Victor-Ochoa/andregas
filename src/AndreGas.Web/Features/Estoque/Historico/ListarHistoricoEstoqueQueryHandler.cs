using AndreGas.Infrastructure;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Estoque.Historico;

public sealed class ListarHistoricoEstoqueQueryHandler(AppDbContext db)
    : IQueryHandler<ListarHistoricoEstoqueQuery, IReadOnlyList<HistoricoEstoqueItem>>
{
    public async ValueTask<IReadOnlyList<HistoricoEstoqueItem>> Handle(
        ListarHistoricoEstoqueQuery query,
        CancellationToken cancellationToken)
    {
        return await db.HistoricosEstoque
            .AsNoTracking()
            .Where(h => h.ProdutoId == query.ProdutoId)
            .OrderByDescending(h => h.Data)
            .ThenByDescending(h => h.Id)
            .Select(h => new HistoricoEstoqueItem(
                h.ProdutoId,
                h.Data,
                h.Tipo,
                h.Descricao,
                h.Motivo,
                h.Usuario,
                h.Quantidade,
                h.VendaId))
            .ToListAsync(cancellationToken);
    }
}