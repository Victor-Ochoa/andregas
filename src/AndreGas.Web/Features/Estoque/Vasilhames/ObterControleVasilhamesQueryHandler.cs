using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Tenancy;

using Mediator;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Estoque.Vasilhames;

public sealed class ObterControleVasilhamesQueryHandler(AppDbContext db, ITenantContext tenant)
    : IQueryHandler<ObterControleVasilhamesQuery, IReadOnlyList<ControleVasilhameItem>>
{
    public async ValueTask<IReadOnlyList<ControleVasilhameItem>> Handle(
        ObterControleVasilhamesQuery query,
        CancellationToken cancellationToken)
    {
        // Exige empresa selecionada (controle de vasilhames é por empresa).
        _ = tenant.EmpresaIdRequerida("listar controle de vasilhames");

        var produtos = await db.Produtos
            .AsNoTracking()
            .Where(p => p.ControlaVasilhame && p.Ativo)
            .OrderBy(p => p.Nome)
            .Select(p => new
            {
                p.Id,
                p.Nome,
                p.QuantidadeVasilhames,
                p.QuantidadeVasilhamesEmprestados,
            })
            .ToListAsync(cancellationToken);

        if (produtos.Count == 0)
        {
            return [];
        }

        var ids = produtos.Select(p => p.Id).ToList();
        var emprestimos = await db.EmprestimosVasilhames
            .AsNoTracking()
            .Where(e => ids.Contains(e.ProdutoId) && e.Quantidade > e.QuantidadeDevolvida)
            .Select(e => new
            {
                e.ClienteId,
                ClienteNome = e.Cliente!.Nome,
                e.ProdutoId,
                Saldo = e.Quantidade - e.QuantidadeDevolvida,
            })
            .ToListAsync(cancellationToken);

        return produtos
            .Select(p => new ControleVasilhameItem(
                p.Id,
                p.Nome,
                p.QuantidadeVasilhames,
                p.QuantidadeVasilhamesEmprestados,
                p.QuantidadeVasilhames + p.QuantidadeVasilhamesEmprestados,
                emprestimos
                    .Where(e => e.ProdutoId == p.Id)
                    .GroupBy(e => (e.ClienteId, e.ClienteNome))
                    .Select(g => new EmprestimoParaClienteItem(
                        g.Key.ClienteId,
                        g.Key.ClienteNome,
                        g.Sum(e => e.Saldo)))
                    .OrderByDescending(c => c.Quantidade)
                    .ThenBy(c => c.ClienteNome)
                    .ToList()))
            .ToList();
    }
}