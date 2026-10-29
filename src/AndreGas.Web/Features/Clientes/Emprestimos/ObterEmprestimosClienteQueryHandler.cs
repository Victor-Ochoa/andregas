using AndreGas.Infrastructure;
using AndreGas.Web.Common;

using Mediator;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Clientes.Emprestimos;

/// <summary>
/// Agrupa os empréstimos em aberto de um cliente por produto. É um <b>helper</b> do detalhe do
/// cliente: a guarda de tenant (<c>EmpresaIdRequerida</c>) fica no handler de entrada
/// (<see cref="AndreGas.Web.Features.Clientes.Detalhe.ObterClienteDetalheQueryHandler"/>), que é
/// quem recebe a requisição do usuário — não a duplica aqui.
/// </summary>
/// <remarks>
/// <b>Não chame este handler diretamente pelo mediator.</b> Por estar registrado como
/// <c>IQueryHandler</c>, um <c>mediator.Send(ObterEmprestimosClienteQuery)</c> o alcançaria sem
/// passar por guarda de tenant alguma. Hoje isso não acontece: ele só é instanciado explicitamente
/// pelo handler de detalhe do cliente, já protegido. A leitura ainda é isolada pelo filtro global
/// do <c>AppDbContext</c> (só enxerga empréstimos da empresa do tenant), mas um chamador futuro
/// direto sem guarda dependeria apenas desse filtro — e um contexto em modo central (tenant nulo)
/// devolveria empréstimos de todas as empresas.
/// </remarks>
public sealed class ObterEmprestimosClienteQueryHandler(AppDbContext db)
    : IQueryHandler<ObterEmprestimosClienteQuery, IReadOnlyList<EmprestimoClienteItem>>
{
    public async ValueTask<IReadOnlyList<EmprestimoClienteItem>> Handle(
        ObterEmprestimosClienteQuery query,
        CancellationToken cancellationToken)
    {
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