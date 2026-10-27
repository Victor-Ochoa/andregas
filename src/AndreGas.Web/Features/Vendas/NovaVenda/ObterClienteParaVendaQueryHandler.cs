using AndreGas.Infrastructure;
using AndreGas.Web.Common;

using Mediator;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Vendas.NovaVenda;

public sealed class ObterClienteParaVendaQueryHandler(AppDbContext db)
    : IQueryHandler<ObterClienteParaVendaQuery, ClienteSugestao?>
{
    public async ValueTask<ClienteSugestao?> Handle(ObterClienteParaVendaQuery query, CancellationToken cancellationToken)
    {
        var cliente = await db.Clientes
            .Where(c => c.Id == query.ClienteId)
            .Select(c => new { c.Id, c.Nome, c.Telefone, c.Endereco, c.SaldoDevedor })
            .FirstOrDefaultAsync(cancellationToken);

        if (cliente is null)
        {
            return null;
        }

        // Valor de entrega da venda mais recente do cliente (0 se ainda não houve venda ou se a
        // última venda não teve entrega) — mesmo critério do BuscarClientePorTermoQueryHandler.
        var ultimaValorEntrega = await VendaQueries.ObtemUltimaEntregaAsync(db, cliente.Id, cancellationToken);

        return new ClienteSugestao(cliente.Id, cliente.Nome, cliente.Telefone, cliente.Endereco, cliente.SaldoDevedor, ultimaValorEntrega);
    }
}