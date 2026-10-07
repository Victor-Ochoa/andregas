using AndreGas.Domain.Entities;
using AndreGas.Infrastructure;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Vendas.NovaVenda;

public sealed class BuscarClientePorTelefoneQueryHandler(AppDbContext db) : IQueryHandler<BuscarClientePorTelefoneQuery, ClienteEncontrado?>
{
    public async ValueTask<ClienteEncontrado?> Handle(BuscarClientePorTelefoneQuery query, CancellationToken cancellationToken)
    {
        var telefoneNormalizado = Cliente.NormalizarTelefone(query.Telefone);
        if (telefoneNormalizado.Length == 0)
        {
            return null;
        }

        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Telefone == telefoneNormalizado, cancellationToken);

        if (cliente is null)
        {
            return null;
        }

        // Valor de entrega da venda mais recente do cliente (0 se ainda não houve venda ou se a
        // última venda não teve entrega) — usado para sugerir o campo na tela de nova venda.
        var ultimaValorEntrega = await db.Vendas
            .Where(v => v.ClienteId == cliente.Id)
            .OrderByDescending(v => v.DataHora)
            .Select(v => v.ValorEntrega)
            .FirstOrDefaultAsync(cancellationToken);

        return new ClienteEncontrado(cliente.Id, cliente.Nome, cliente.Endereco, cliente.SaldoDevedor, ultimaValorEntrega);
    }
}
