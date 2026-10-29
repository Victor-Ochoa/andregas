using AndreGas.Domain.Entities;
using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Tenancy;

using Mediator;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Central.InativarEmpresa;

/// <summary>
/// Inativa (<see cref="Empresa.Desativar"/>) ou reativa (<see cref="Empresa.Ativar"/>) uma empresa.
/// </summary>
/// <remarks>
/// <b>Não</b> inativa os usuários em cascata de propósito: reativar a empresa precisa devolver o
/// acesso de todos os operadores, sem uma segunda operação. O bloqueio de login é feito pelo
/// <c>LoginCommandHandler</c>, que já valida <c>Empresa.Ativo</c>.
/// </remarks>
public sealed class InativarEmpresaCommandHandler(AppDbContext db, ITenantContext tenant)
    : ICommandHandler<InativarEmpresaCommand, bool>
{
    public async ValueTask<bool> Handle(InativarEmpresaCommand command, CancellationToken cancellationToken)
    {
        // Limite real de segurança: a política da rota não basta, o handler reforça no servidor.
        if (!tenant.EhSuperAdmin)
        {
            throw new InvalidOperationException("Somente o super administrador da central pode inativar empresas.");
        }

        var empresa = await db.Set<Empresa>().FirstOrDefaultAsync(e => e.Id == command.EmpresaId, cancellationToken);
        if (empresa is null)
        {
            return false;
        }

        if (command.Ativar)
        {
            empresa.Ativar();
        }
        else
        {
            empresa.Desativar();
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}