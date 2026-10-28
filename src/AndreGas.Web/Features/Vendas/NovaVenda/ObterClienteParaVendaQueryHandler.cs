using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Tenancy;

using Mediator;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Vendas.NovaVenda;

public sealed class ObterClienteParaVendaQueryHandler(AppDbContext db, ITenantContext tenant)
    : IQueryHandler<ObterClienteParaVendaQuery, ClienteSugestao?>
{
    public async ValueTask<ClienteSugestao?> Handle(ObterClienteParaVendaQuery query, CancellationToken cancellationToken)
    {
        // Exige empresa selecionada (cliente localizado pelo filtro global do tenant).
        _ = tenant.EmpresaIdRequerida("obter cliente para venda");

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
        var ultimaValorEntrega = await db.Vendas
            .Where(v => v.ClienteId == cliente.Id && v.ExcluidaEm == null)
            .OrderByDescending(v => v.DataHora)
            .Select(v => (decimal?)v.ValorEntrega)
            .FirstOrDefaultAsync(cancellationToken) ?? 0m;

        return new ClienteSugestao(cliente.Id, cliente.Nome, cliente.Telefone, cliente.Endereco, cliente.SaldoDevedor, ultimaValorEntrega);
    }
}