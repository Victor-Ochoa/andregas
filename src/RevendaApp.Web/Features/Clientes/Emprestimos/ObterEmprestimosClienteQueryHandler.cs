using Mediator;

using Microsoft.EntityFrameworkCore;

using RevendaApp.Infrastructure;
using RevendaApp.Infrastructure.Tenancy;
using RevendaApp.Web.Common;

namespace RevendaApp.Web.Features.Clientes.Emprestimos;

/// <summary>
/// Agrupa os empréstimos em aberto de um cliente por produto.
/// </summary>
/// <remarks>
/// <b>Guarda de tenant própria:</b> este handler é um <c>IQueryHandler</c> alcançável por
/// <c>mediator.Send(ObterEmprestimosClienteQuery)</c>, então não pode depender de o chamador ter
/// guardado o tenant. Hoje ele é instanciado explicitamente pelo detalhe do cliente (que também
/// guarda), mas um chamador futuro direto rodaria em modo central (tenant nulo) e o filtro global do
/// <c>AppDbContext</c> — que em modo central não filtra nada — devolveria empréstimos de todas as
/// empresas. Por isso a primeira instrução exige a empresa selecionada.
/// </remarks>
public sealed class ObterEmprestimosClienteQueryHandler(AppDbContext db, ITenantContext tenant)
    : IQueryHandler<ObterEmprestimosClienteQuery, IReadOnlyList<EmprestimoClienteItem>>
{
    public async ValueTask<IReadOnlyList<EmprestimoClienteItem>> Handle(
        ObterEmprestimosClienteQuery query,
        CancellationToken cancellationToken)
    {
        _ = tenant.EmpresaIdRequerida("obter empréstimos do cliente");

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