using AndreGas.Infrastructure;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Estoque.Editar;

public sealed class ObterProdutoQueryHandler(AppDbContext db) : IQueryHandler<ObterProdutoQuery, ObterProdutoResult?>
{
    public async ValueTask<ObterProdutoResult?> Handle(ObterProdutoQuery query, CancellationToken cancellationToken)
    {
        return await db.Produtos
            .Where(p => p.Id == query.ProdutoId)
            .Select(p => new ObterProdutoResult(
                p.Id,
                p.Nome,
                p.Tipo,
                p.PrecoVenda,
                p.PrecoCusto,
                p.PrecoGasDoPovo,
                p.EstoqueMinimo,
                p.Ativo))
            .FirstOrDefaultAsync(cancellationToken);
    }
}