using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Tenancy;

using Mediator;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Estoque.Vasilhames;

public sealed class ObterClientesComEmprestimoProdutoQueryHandler(AppDbContext db, ITenantContext tenant)
    : IQueryHandler<ObterClientesComEmprestimoProdutoQuery, IReadOnlyList<ClienteComEmprestimoItem>>
{
    public async ValueTask<IReadOnlyList<ClienteComEmprestimoItem>> Handle(
        ObterClientesComEmprestimoProdutoQuery query,
        CancellationToken cancellationToken)
    {
        // Exige empresa selecionada (empréstimos são por empresa).
        _ = tenant.EmpresaIdRequerida("listar clientes com empréstimo");

        var emprestimos = await db.EmprestimosVasilhames
            .AsNoTracking()
            .Where(e => e.ProdutoId == query.ProdutoId && e.Quantidade > e.QuantidadeDevolvida)
            .Select(e => new
            {
                e.ClienteId,
                ClienteNome = e.Cliente!.Nome,
                Telefone = e.Cliente!.Telefone,
                Saldo = e.Quantidade - e.QuantidadeDevolvida,
            })
            .ToListAsync(cancellationToken);

        return emprestimos
            .GroupBy(e => (e.ClienteId, e.ClienteNome, e.Telefone))
            .Select(g => new ClienteComEmprestimoItem(
                g.Key.ClienteId,
                g.Key.ClienteNome,
                g.Key.Telefone,
                g.Sum(e => e.Saldo)))
            .OrderBy(c => c.ClienteNome)
            .ToList();
    }
}