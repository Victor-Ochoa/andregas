using AndreGas.Infrastructure;
using AndreGas.Web.Common;
using AndreGas.Web.Features.Clientes.Emprestimos;

using Mediator;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Clientes.Detalhe;

public sealed class ObterClienteDetalheQueryHandler(AppDbContext db) : IQueryHandler<ObterClienteDetalheQuery, ClienteDetalheResult?>
{
    public async ValueTask<ClienteDetalheResult?> Handle(ObterClienteDetalheQuery query, CancellationToken cancellationToken)
    {
        var cliente = await db.Clientes.FindAsync([query.ClienteId], cancellationToken);
        if (cliente is null)
        {
            return null;
        }

        // Reaproveita o agrupamento de empréstimos em aberto do slice de Empréstimos (Task 8).
        var emprestimosHandler = new ObterEmprestimosClienteQueryHandler(db);
        var emprestimos = await emprestimosHandler.Handle(
            new ObterEmprestimosClienteQuery(query.ClienteId),
            cancellationToken);

        var vendas = await db.Vendas
            .Where(v => v.ClienteId == query.ClienteId && v.ExcluidaEm == null)
            .Include(v => v.Itens)
            .OrderByDescending(v => v.DataHora)
            .ToListAsync(cancellationToken);

        var historico = vendas
            .Select(v => new VendaHistoricoItem(v.Id, BrasilTimeZone.ParaBrasilia(v.DataHora), v.FormaPagamento, v.Status, v.ValorTotal, v.LucroTotal, v.ValorEntrega))
            .ToList();

        // Agrega por produto a quantidade total comprada (todas as vendas não excluídas,
        // incluindo itens empréstimo). Só afeta a exibição na tela do cliente.
        var produtosComprados = vendas
            .SelectMany(v => v.Itens)
            .GroupBy(i => new ProdutoCompradoKey(i.ProdutoId, i.Produto?.Nome ?? "Produto removido"))
            .OrderByDescending(g => g.Sum(i => i.Quantidade))
            .ThenBy(g => g.Key.Nome, StringComparer.Ordinal)
            .Select(g => new ProdutoCompradoItem(g.Key.ProdutoId, g.Key.Nome, g.Sum(i => i.Quantidade)))
            .ToList();

        var pagamentos = await db.Pagamentos
            .Where(p => p.ClienteId == query.ClienteId)
            .OrderByDescending(p => p.Data)
            .ToListAsync(cancellationToken);

        var historicoPagamentos = pagamentos
            .Select(p => new PagamentoHistoricoItem(p.Id, BrasilTimeZone.ParaBrasilia(p.Data), p.Valor, p.FormaPagamento, p.Observacao))
            .ToList();

        return new ClienteDetalheResult(
            cliente.Id,
            cliente.Nome,
            cliente.Telefone,
            cliente.Endereco,
            cliente.SaldoDevedor,
            cliente.Ativo,
            historico,
            historicoPagamentos,
            emprestimos,
            produtosComprados);
    }

    private sealed record ProdutoCompradoKey(Guid ProdutoId, string Nome);
}