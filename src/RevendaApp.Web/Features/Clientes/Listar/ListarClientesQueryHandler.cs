using Mediator;

using Microsoft.EntityFrameworkCore;

using RevendaApp.Infrastructure;
using RevendaApp.Infrastructure.Tenancy;

namespace RevendaApp.Web.Features.Clientes.Listar;

public sealed class ListarClientesQueryHandler(AppDbContext db, ITenantContext tenant) : IQueryHandler<ListarClientesQuery, IReadOnlyList<ClienteListItem>>
{
    public async ValueTask<IReadOnlyList<ClienteListItem>> Handle(ListarClientesQuery query, CancellationToken cancellationToken)
    {
        // Exige empresa selecionada (central não lista clientes de todas as empresas).
        _ = tenant.EmpresaIdRequerida("listar clientes");

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