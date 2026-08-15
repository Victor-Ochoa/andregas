using AndreGas.Infrastructure;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Estoque.Listar;

public sealed class ListarProdutosQueryHandler(AppDbContext db) : IQueryHandler<ListarProdutosQuery, IReadOnlyList<ProdutoListItem>>
{
    public async ValueTask<IReadOnlyList<ProdutoListItem>> Handle(ListarProdutosQuery query, CancellationToken cancellationToken)
    {
        return await db.Produtos
            .OrderBy(p => p.Nome)
            .Select(p => new ProdutoListItem(
                p.Id,
                p.Nome,
                p.Tipo,
                p.PrecoVenda,
                p.PrecoCusto,
                p.PrecoGasDoPovo,
                p.QuantidadeEstoque,
                p.EstoqueMinimo,
                p.QuantidadeEstoque <= p.EstoqueMinimo))
            .ToListAsync(cancellationToken);
    }
}
