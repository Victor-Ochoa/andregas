using Mediator;

using Microsoft.EntityFrameworkCore;

using RevendaApp.Infrastructure;
using RevendaApp.Infrastructure.Tenancy;

namespace RevendaApp.Web.Features.Estoque.Editar;

public sealed class ObterProdutoQueryHandler(AppDbContext db, ITenantContext tenant) : IQueryHandler<ObterProdutoQuery, ObterProdutoResult?>
{
    public async ValueTask<ObterProdutoResult?> Handle(ObterProdutoQuery query, CancellationToken cancellationToken)
    {
        // Exige empresa selecionada (central não lê produto de outra empresa).
        _ = tenant.EmpresaIdRequerida("obter produto");

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
                p.Ativo,
                p.ControlaVasilhame,
                p.QuantidadeVasilhames,
                p.QuantidadeVasilhamesEmprestados,
                p.AvisarRecompra,
                p.DiasPadraoRecompra))
            .FirstOrDefaultAsync(cancellationToken);
    }
}