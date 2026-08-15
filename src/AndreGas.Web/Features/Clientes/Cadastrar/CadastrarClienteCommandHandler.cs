using AndreGas.Domain.Entities;
using AndreGas.Infrastructure;
using Mediator;

namespace AndreGas.Web.Features.Clientes.Cadastrar;

public sealed class CadastrarClienteCommandHandler(AppDbContext db) : ICommandHandler<CadastrarClienteCommand, Guid>
{
    public async ValueTask<Guid> Handle(CadastrarClienteCommand command, CancellationToken cancellationToken)
    {
        var cliente = new Cliente(command.Nome, command.Telefone, command.Endereco);

        db.Clientes.Add(cliente);
        await db.SaveChangesAsync(cancellationToken);

        return cliente.Id;
    }
}
