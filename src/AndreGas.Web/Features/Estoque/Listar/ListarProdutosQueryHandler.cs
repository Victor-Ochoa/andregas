using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Tenancy;

using Mediator;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Estoque.Listar;

public sealed class ListarProdutosQueryHandler(AppDbContext db, ITenantContext tenant) : IQueryHandler<ListarProdutosQuery, IReadOnlyList<ProdutoListItem>>
{
    public async ValueTask<IReadOnlyList<ProdutoListItem>> Handle(ListarProdutosQuery query, CancellationToken cancellationToken)
    {
        // Exige empresa selecionada (central não lista o catálogo de todas as empresas).
        _ = tenant.EmpresaIdRequerida("listar produtos");

        return await db.Produtos
            .Where(p => query.IncluirInativos || p.Ativo)
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
                p.QuantidadeEstoque <= p.EstoqueMinimo,
                p.Ativo,
                p.ControlaVasilhame,
                p.QuantidadeVasilhames,
                p.QuantidadeVasilhamesEmprestados,
                p.QuantidadeVasilhames + p.QuantidadeVasilhamesEmprestados))
            .ToListAsync(cancellationToken);
    }
}