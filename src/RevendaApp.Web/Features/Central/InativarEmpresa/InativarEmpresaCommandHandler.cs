using Mediator;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

using RevendaApp.Domain.Entities;
using RevendaApp.Infrastructure;
using RevendaApp.Infrastructure.Tenancy;
using RevendaApp.Web.Common;

namespace RevendaApp.Web.Features.Central.InativarEmpresa;

/// <summary>
/// Inativa (<see cref="Empresa.Desativar"/>) ou reativa (<see cref="Empresa.Ativar"/>) uma empresa.
/// </summary>
/// <remarks>
/// <para>
/// <b>Não</b> inativa os usuários em cascata de propósito: reativar a empresa precisa devolver o
/// acesso de todos os operadores, sem uma segunda operação. O bloqueio de login é feito pelo
/// <c>LoginCommandHandler</c>, que já valida <c>Empresa.Ativo</c>.
/// </para>
/// <para>
/// <b>Auditoria:</b> grava <c>EmpresaInativada</c> ou <c>EmpresaReativada</c> (conforme
/// <c>command.Ativar</c>) no <b>mesmo</b> <c>SaveChanges</c>, com o estado antes → depois.
/// </para>
/// </remarks>
public sealed class InativarEmpresaCommandHandler(
    AppDbContext db,
    ITenantContext tenant,
    AuthenticationStateProvider? authStateProvider = null)
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

        var estavaAtiva = empresa.Ativo;

        if (command.Ativar)
        {
            empresa.Ativar();
        }
        else
        {
            empresa.Desativar();
        }

        var (usuarioNome, usuarioId) = await UsuarioAtual.ObterAsync(authStateProvider, db, cancellationToken);
        var acao = command.Ativar
            ? RegistroAuditoria.Acoes.EmpresaReativada
            : RegistroAuditoria.Acoes.EmpresaInativada;
        var detalhes = $"Status: {DescreverStatus(estavaAtiva)} → {DescreverStatus(empresa.Ativo)}";

        db.RegistrosAuditoria.Add(new RegistroAuditoria(
            empresa.Id,
            usuarioId,
            usuarioNome,
            acao,
            detalhes,
            RegistroAuditoria.Origens.Central));

        // Um único SaveChanges: alteração de status + auditoria na mesma transação implícita.
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string DescreverStatus(bool ativa) => ativa ? "Ativa" : "Inativa";
}