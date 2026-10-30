using Mediator;

using RevendaApp.Infrastructure;
using RevendaApp.Infrastructure.Tenancy;

namespace RevendaApp.Web.Features.Clientes.Detalhe;

public sealed class AtualizarClienteCommandHandler(AppDbContext db, ITenantContext tenant) : ICommandHandler<AtualizarClienteCommand, bool>
{
    public async ValueTask<bool> Handle(AtualizarClienteCommand command, CancellationToken cancellationToken)
    {
        // Exige empresa selecionada (cliente localizado pelo filtro global do tenant).
        _ = tenant.EmpresaIdRequerida("atualizar cliente");

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