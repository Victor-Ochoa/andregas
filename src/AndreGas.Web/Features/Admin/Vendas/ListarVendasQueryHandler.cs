using AndreGas.Infrastructure;
using AndreGas.Web.Common;

using Mediator;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Admin.Vendas;

public sealed class ListarVendasQueryHandler(AppDbContext db) : IQueryHandler<ListarVendasQuery, IReadOnlyList<VendaAdminListItem>>
{
    public async ValueTask<IReadOnlyList<VendaAdminListItem>> Handle(ListarVendasQuery query, CancellationToken cancellationToken)
    {
        // Somente vendas ativas (exclusão lógica): excluídas ficam no banco, mas não contam.
        var vendas = await db.Vendas
            .Where(v => v.ExcluidaEm == null)
            .Include(v => v.Itens)
            .Include(v => v.Cliente)
            .OrderByDescending(v => v.DataHora)
            .ToListAsync(cancellationToken);

        // Nome do vendedor (usuário que registrou a venda), para exibir na listagem administrativa.
        var vendedorIds = vendas
            .Select(v => v.VendedorId)
            .Where(id => id is not null)
            .Cast<Guid>()
            .Distinct()
            .ToArray();

        var nomesVendedores = vendedorIds.Length == 0
            ? new Dictionary<Guid, string?>()
            : await db.Users
                .Where(u => vendedorIds.Contains(u.Id))
                .Select(u => new { u.Id, Nome = u.Nome ?? u.Email })
                .ToDictionaryAsync(u => u.Id, u => u.Nome, cancellationToken);

        return vendas
            .Select(v => new VendaAdminListItem(
                v.Id,
                BrasilTimeZone.ParaBrasilia(v.DataHora),
                v.Cliente?.Nome ?? string.Empty,
                v.Cliente?.Telefone,
                v.FormaPagamento,
                v.Status,
                v.ValorTotal,
                v.LucroTotal,
                v.Desconto,
                v.ValorEntrega,
                v.Itens.Sum(i => i.Quantidade),
                v.VendedorId is Guid vid ? nomesVendedores.GetValueOrDefault(vid) : null))
            .ToList();
    }
}