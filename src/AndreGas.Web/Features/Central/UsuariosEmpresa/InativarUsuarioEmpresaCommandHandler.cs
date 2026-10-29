using System.Data;

using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Identity;
using AndreGas.Infrastructure.Tenancy;
using AndreGas.Web.Common;

using Mediator;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AndreGas.Web.Features.Central.UsuariosEmpresa;

/// <summary>
/// Inativa/reativa um usuário de uma empresa pela central.
/// </summary>
/// <remarks>
/// <para>
/// <b>Escopo do tenant:</b> o usuário é carregado com <c>EmpresaId == command.EmpresaId</c> na
/// própria consulta. Um id de usuário de outra empresa simplesmente não é encontrado — o handler
/// nunca aceita um <c>UsuarioId</c> avulso.
/// </para>
/// <para>
/// <b>Último administrador (atômico):</b> inativar o último <c>Admin</c> ativo deixaria a empresa
/// sem quem a administre. A contagem e a gravação rodam na <b>mesma transação</b>, em nível
/// <see cref="IsolationLevel.Serializable"/>: sob <c>ReadCommitted</c> duas desativações
/// simultâneas dos dois únicos admins passariam as duas (check-then-act clássico) e a empresa
/// ficaria sem administrador. O conflito de serialização faz uma das transações falhar e ser
/// reexecutada pela execution strategy, que então enxerga o estado novo e recusa.
/// </para>
/// <para>
/// <b>Revogação da sessão viva:</b> ao <b>inativar</b>, o <c>SecurityStamp</c> é renovado via
/// <see cref="UserManager{TUser}.UpdateSecurityStampAsync"/>. Sem isso, o cookie já emitido
/// continuaria válido até o <c>SecurityStampValidator</c> rodar (30 min) ou o navegador fechar: um
/// usuário recém-desativado seguiria operando. A reativação <b>não</b> mexe no stamp (o usuário
/// simplesmente volta a poder logar).
/// </para>
/// <para>
/// <b>Desativação da empresa não é cascata:</b> inativar a <b>empresa</b> não renova o stamp de
/// ninguém de propósito — reativar precisa devolver o acesso a todos sem uma segunda operação, e o
/// bloqueio de login já é do <c>LoginCommandHandler</c>.
/// </para>
/// </remarks>
public sealed class InativarUsuarioEmpresaCommandHandler(
    AppDbContext db,
    ITenantContext tenant,
    UserManager<ApplicationUser> userManager)
    : ICommandHandler<InativarUsuarioEmpresaCommand, InativarUsuarioEmpresaResult>
{
    public async ValueTask<InativarUsuarioEmpresaResult> Handle(InativarUsuarioEmpresaCommand command, CancellationToken cancellationToken)
    {
        // Limite real de segurança: a política da rota não basta, o handler reforça no servidor.
        if (!tenant.EhSuperAdmin)
        {
            throw new InvalidOperationException("Somente o super administrador da central pode inativar usuários de uma empresa.");
        }

        var resultado = await TransacaoComRetry.ExecutarAsync(
            db,
            transacao => ExecutarNaTransacaoAsync(command, transacao, cancellationToken),
            cancellationToken,
            nivelIsolamento: command.Ativo ? null : IsolationLevel.Serializable);

        if (!resultado.Sucesso)
        {
            return resultado;
        }

        // Fora da transação: o UpdateSecurityStampAsync faz o próprio SaveChanges no UserManager.
        // Só na inativação — reativar não precisa (e não deve) derrubar sessão de ninguém.
        if (!command.Ativo)
        {
            var usuario = await db.Users.FirstAsync(
                u => u.Id == command.UsuarioId && u.EmpresaId == command.EmpresaId, cancellationToken);

            await userManager.UpdateSecurityStampAsync(usuario);
        }

        return resultado;
    }

    private async Task<InativarUsuarioEmpresaResult> ExecutarNaTransacaoAsync(
        InativarUsuarioEmpresaCommand command,
        IDbContextTransaction transacao,
        CancellationToken cancellationToken)
    {
        // A entidade foi carregada na tentativa anterior (retry): recomeça limpo.
        db.ChangeTracker.Clear();

        // Filtro por empresa na própria consulta: id de usuário de outra empresa não é encontrado.
        var usuario = await db.Users
            .FirstOrDefaultAsync(u => u.Id == command.UsuarioId && u.EmpresaId == command.EmpresaId, cancellationToken);

        if (usuario is null)
        {
            return InativarUsuarioEmpresaResult.Falha("Usuário não encontrado nesta empresa.");
        }

        if (!command.Ativo && await EhUltimoAdminAtivoAsync(command.EmpresaId, usuario.Id, cancellationToken))
        {
            return InativarUsuarioEmpresaResult.Falha(
                "Não é possível inativar o último administrador ativo da empresa. "
                + "Crie ou reative outro administrador antes.");
        }

        usuario.Ativo = command.Ativo;
        await db.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);

        return InativarUsuarioEmpresaResult.Ok();
    }

    /// <summary>
    /// Verdadeiro quando o usuário é <c>Admin</c> e não existe nenhum outro <c>Admin</c> ativo na
    /// mesma empresa. Usuários de empresa não têm <c>SuperAdmin</c>, então a contagem considera só
    /// a role <c>Admin</c>. O join é restrito aos usuários <b>da empresa</b> (não varre as demais).
    /// </summary>
    private async Task<bool> EhUltimoAdminAtivoAsync(Guid empresaId, Guid usuarioId, CancellationToken cancellationToken)
    {
        var ehAdmin = await (from ur in db.Set<IdentityUserRole<Guid>>()
                             join r in db.Set<ApplicationRole>() on ur.RoleId equals r.Id
                             join u in db.Users on ur.UserId equals u.Id
                             where u.EmpresaId == empresaId && ur.UserId == usuarioId && r.Name == ApplicationRole.Admin
                             select ur.UserId)
            .AnyAsync(cancellationToken);

        if (!ehAdmin)
        {
            // Não é administrador: inativar não deixa a empresa sem admin.
            return false;
        }

        var outrosAdminsAtivos = await (from ur in db.Set<IdentityUserRole<Guid>>()
                                        join r in db.Set<ApplicationRole>() on ur.RoleId equals r.Id
                                        join u in db.Users on ur.UserId equals u.Id
                                        where u.EmpresaId == empresaId
                                            && u.Ativo
                                            && u.Id != usuarioId
                                            && r.Name == ApplicationRole.Admin
                                        select u.Id)
            .AnyAsync(cancellationToken);

        return !outrosAdminsAtivos;
    }
}