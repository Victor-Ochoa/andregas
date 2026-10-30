using Mediator;

using Microsoft.EntityFrameworkCore;

using RevendaApp.Infrastructure;
using RevendaApp.Infrastructure.Tenancy;

namespace RevendaApp.Web.Features.Central.ListarProdutosEmpresa;

/// <summary>
/// Lista os produtos de uma empresa para a central (leitura). O filtro é <b>explícito</b> por
/// <c>EmpresaId</c> — funciona igual nos modos central (sem filtro global) e de empresa, sem
/// depender de <c>IgnoreQueryFilters()</c>.
/// </summary>
public sealed class ListarProdutosEmpresaQueryHandler(AppDbContext db, ITenantContext tenant)
    : IQueryHandler<ListarProdutosEmpresaQuery, IReadOnlyList<ProdutoEmpresaItem>>
{
    public async ValueTask<IReadOnlyList<ProdutoEmpresaItem>> Handle(ListarProdutosEmpresaQuery query, CancellationToken cancellationToken)
    {
        // Limite real de segurança: a política da rota não basta, o handler reforça no servidor.
        if (!tenant.EhSuperAdmin)
        {
            throw new InvalidOperationException("Somente o super administrador da central pode ver os produtos de uma empresa.");
        }

        return await db.Produtos
            .Where(p => p.EmpresaId == query.EmpresaId)
            .OrderBy(p => p.Nome)
            .Select(p => new ProdutoEmpresaItem(
                p.Id,
                p.Nome,
                p.Tipo,
                p.PrecoVenda,
                p.QuantidadeEstoque,
                p.Ativo))
            .ToListAsync(cancellationToken);
    }
}