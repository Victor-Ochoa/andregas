using AndreGas.Domain.Entities;
using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Tenancy;

using Mediator;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Central.EditarEmpresa;

/// <summary>
/// Edita os dados de cadastro de uma empresa. Retorna <c>false</c> quando a empresa não existe.
/// </summary>
/// <remarks>
/// A tabela <c>Empresas</c> não tem filtro global, então a busca funciona igual nos modos central
/// e de empresa; ainda assim o handler exige super admin — é o limite real de segurança.
/// </remarks>
public sealed class EditarEmpresaCommandHandler(AppDbContext db, ITenantContext tenant)
    : ICommandHandler<EditarEmpresaCommand, bool>
{
    public async ValueTask<bool> Handle(EditarEmpresaCommand command, CancellationToken cancellationToken)
    {
        // Limite real de segurança: a política da rota não basta, o handler reforça no servidor.
        if (!tenant.EhSuperAdmin)
        {
            throw new InvalidOperationException("Somente o super administrador da central pode editar empresas.");
        }

        var empresa = await db.Set<Empresa>().FirstOrDefaultAsync(e => e.Id == command.EmpresaId, cancellationToken);
        if (empresa is null)
        {
            return false;
        }

        // Identificador não é passado de propósito: é imutável após a criação.
        empresa.AtualizarDados(
            command.Nome,
            command.Cnpj,
            command.Endereco,
            command.Numero,
            command.Cep,
            command.Bairro,
            command.Cidade,
            command.Estado,
            command.Telefones,
            command.CorPrimaria,
            command.Logo);

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}