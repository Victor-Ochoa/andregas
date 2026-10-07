using AndreGas.Infrastructure;
using AndreGas.Web.Common;
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

        var vendas = await db.Vendas
            .Where(v => v.ClienteId == query.ClienteId)
            .Include(v => v.Itens)
            .OrderByDescending(v => v.DataHora)
            .ToListAsync(cancellationToken);

        var historico = vendas
            .Select(v => new VendaHistoricoItem(v.Id, BrasilTimeZone.ParaBrasilia(v.DataHora), v.FormaPagamento, v.Status, v.ValorTotal, v.LucroTotal, v.ValorEntrega))
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
            historicoPagamentos);
    }
}
