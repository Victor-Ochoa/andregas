using AndreGas.Infrastructure;
using Mediator;

namespace AndreGas.Web.Features.Clientes.Detalhe;

public sealed class AtualizarClienteCommandHandler(AppDbContext db) : ICommandHandler<AtualizarClienteCommand, bool>
{
    public async ValueTask<bool> Handle(AtualizarClienteCommand command, CancellationToken cancellationToken)
    {
        var cliente = await db.Clientes.FindAsync([command.Id], cancellationToken);
        if (cliente is null)
        {
            return false;
        }

        cliente.AtualizarDados(command.Nome, command.Telefone, command.Endereco);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }
}
