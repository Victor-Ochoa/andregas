using Mediator;

using RevendaApp.Domain.Entities;
using RevendaApp.Infrastructure;
using RevendaApp.Infrastructure.Tenancy;

namespace RevendaApp.Web.Features.Clientes.Cadastrar;

public sealed class CadastrarClienteCommandHandler(AppDbContext db, ITenantContext tenant) : ICommandHandler<CadastrarClienteCommand, Guid>
{
    public async ValueTask<Guid> Handle(CadastrarClienteCommand command, CancellationToken cancellationToken)
    {
        // Exige empresa selecionada: o cliente pertence sempre a uma empresa.
        _ = tenant.EmpresaIdRequerida("cadastrar cliente");

        var cliente = new Cliente(command.Nome, command.Telefone, command.Endereco);

        db.Clientes.Add(cliente);
        await db.SaveChangesAsync(cancellationToken);

        return cliente.Id;
    }
}