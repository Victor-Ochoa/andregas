using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Tenancy;
using AndreGas.Web.Common;

using Mediator;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Clientes.Emprestimos;

public sealed class ObterEmprestimosClienteQueryHandler(AppDbContext db, ITenantContext tenant)
    : IQueryHandler<ObterEmprestimosClienteQuery, IReadOnlyList<EmprestimoClienteItem>>
{
    public async ValueTask<IReadOnlyList<EmprestimoClienteItem>> Handle(
        ObterEmprestimosClienteQuery query,
        CancellationToken cancellationToken)
    {
        // Exige empresa selecionada (empréstimos são por empresa).
        _ = tenant.EmpresaIdRequerida("listar empréstimos do cliente");

        var emprestimos = await db.EmprestimosVasilhames
            .AsNoTracking()
            .Where(e => e.ClienteId == query.ClienteId && e.DevolvidoEm == null)
            .Select(e => new { e.ProdutoId, e.Produto!.Nome, Saldo = e.Quantidade - e.QuantidadeDevolvida, e.Data })
            .ToListAsync(cancellationToken);

        // Converte o UTC gravado no banco para o fuso de Brasília APÓS materializar (o EF não
        // traduz a conversão para SQL).
        return emprestimos
            .GroupBy(e => e.ProdutoId)
            .Select(g => new EmprestimoClienteItem(
                g.Key,
                g.First().Nome,
                g.Sum(e => e.Saldo),
                BrasilTimeZone.ParaBrasilia(g.Min(e => e.Data))))
            .ToList();
    }
}