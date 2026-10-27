using AndreGas.Infrastructure;

using Mediator;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Clientes.Listar;

public sealed class ListarClientesQueryHandler(AppDbContext db) : IQueryHandler<ListarClientesQuery, IReadOnlyList<ClienteListItem>>
{
    public async ValueTask<IReadOnlyList<ClienteListItem>> Handle(ListarClientesQuery query, CancellationToken cancellationToken)
    {
        return await db.Clientes
            .AsNoTracking()
            .OrderBy(c => c.Nome)
            .Select(c => new ClienteListItem(
                c.Id,
                c.Nome,
                c.Telefone,
                c.Endereco,
                c.SaldoDevedor,
                c.Ativo,
                db.EmprestimosVasilhames
                    .Where(e => e.ClienteId == c.Id && e.DevolvidoEm == null)
                    .Sum(e => e.Quantidade - e.QuantidadeDevolvida)))
            .ToListAsync(cancellationToken);
    }
}