using AndreGas.Infrastructure;
using AndreGas.Web.Common;
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
        var registros = await db.HistoricosEstoque
            .AsNoTracking()
            .Where(h => h.ProdutoId == query.ProdutoId)
            .OrderByDescending(h => h.Data)
            .ThenByDescending(h => h.Id)
            .ToListAsync(cancellationToken);

        // Converte o UTC gravado no banco para o fuso de Brasília APÓS materializar (o EF não
        // traduz a conversão para SQL).
        return registros
            .Select(h => new HistoricoEstoqueItem(
                h.ProdutoId,
                BrasilTimeZone.ParaBrasilia(h.Data),
                h.Tipo,
                h.Descricao,
                h.Motivo,
                h.Usuario,
                h.Quantidade,
                h.VendaId))
            .ToList();
    }
}