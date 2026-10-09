using AndreGas.Infrastructure;

using Mediator;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Vendas.NovaVenda;

/// <summary>
/// Consulta os preços personalizados (acordos) de um cliente, mapeados por <c>ProdutoId</c>, para
/// a tela de nova venda pré-preencher o valor unitário de cada item.
/// </summary>
public sealed record ObterPrecosPersonalizadosQuery(Guid ClienteId) : IQuery<IReadOnlyDictionary<Guid, decimal>>;

public sealed class ObterPrecosPersonalizadosQueryHandler(AppDbContext db)
    : IQueryHandler<ObterPrecosPersonalizadosQuery, IReadOnlyDictionary<Guid, decimal>>
{
    public async ValueTask<IReadOnlyDictionary<Guid, decimal>> Handle(
        ObterPrecosPersonalizadosQuery query, CancellationToken cancellationToken)
    {
        return await db.PrecosPersonalizadosClientes
            .Where(p => p.ClienteId == query.ClienteId)
            .ToDictionaryAsync(p => p.ProdutoId, p => p.Preco, cancellationToken);
    }
}