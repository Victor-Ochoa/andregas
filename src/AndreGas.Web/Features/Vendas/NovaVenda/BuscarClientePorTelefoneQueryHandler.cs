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

        return cliente is null
            ? null
            : new ClienteEncontrado(cliente.Id, cliente.Nome, cliente.Endereco, cliente.SaldoDevedor);
    }
}
