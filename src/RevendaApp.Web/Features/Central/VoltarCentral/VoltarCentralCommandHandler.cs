using System.Security.Claims;

using Mediator;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using RevendaApp.Domain.Entities;
using RevendaApp.Infrastructure;
using RevendaApp.Infrastructure.Identity;
using RevendaApp.Web.Common;
using RevendaApp.Web.Common.Authorization;
using RevendaApp.Web.Common.Tenancy;

namespace RevendaApp.Web.Features.Central.VoltarCentral;

/// <summary>
/// Restaura a sessão do super admin original a partir de uma sessão impersonada.
/// </summary>
/// <remarks>
/// <para>
/// <b>Fonte do id (segurança):</b> o super admin original vem <b>apenas</b> da claim assinada
/// <see cref="TenantContext.ClaimSuperAdminOriginalId"/> do cookie. Nada de id no corpo/query —
/// seria forjável por qualquer usuário autenticado.
/// </para>
/// <para>
/// <b>Condições (decisão 5 da Task 9):</b> age somente se (a) a claim
/// <see cref="TenantContext.ClaimImpersonando"/> for <c>"true"</c>, (b) o principal <b>atual</b>
/// carregar a claim <see cref="TenantContext.ClaimEmpresaId"/> (todo principal impersonado é de
/// empresa — barra um principal de central que só tenha as claims de impersonation) e seu
/// <c>NameIdentifier</c> for <b>diferente</b> do <see cref="TenantContext.ClaimSuperAdminOriginalId"/>
/// (o super admin não impersona a si mesmo) e (c) o Guid da claim apontar para um usuário que
/// <b>ainda</b> é super admin, <b>ativo</b> e <b>sem empresa</b>. Qualquer outra situação devolve
/// falha — o endpoint responde 403/redirect para <c>/sem-autorizacao</c>.
/// </para>
/// </remarks>
public sealed class VoltarCentralCommandHandler(
    IHttpContextAccessor httpContextAccessor,
    UserManager<ApplicationUser> userManager,
    IPrincipalDoUsuario principalDoUsuario,
    AppDbContext db)
    : ICommandHandler<VoltarCentralCommand, VoltarCentralResult>
{
    private const string MensagemSemImpersonacao =
        "Não há uma sessão de impersonation ativa para encerrar.";

    public async ValueTask<VoltarCentralResult> Handle(VoltarCentralCommand command, CancellationToken cancellationToken)
    {
        var principalAtual = httpContextAccessor.HttpContext?.User;
        if (principalAtual?.Identity is not { IsAuthenticated: true })
        {
            return VoltarCentralResult.Failure("Usuário não autenticado.");
        }

        if (!LerBooleano(principalAtual, TenantContext.ClaimImpersonando))
        {
            return VoltarCentralResult.Failure(MensagemSemImpersonacao);
        }

        // O principal ATUAL precisa ser de fato um principal impersonado: um usuário de empresa
        // (claim EmpresaId) e cujo NameIdentifier não é o do super admin original. Isso fecha o
        // caminho latente em que uma futura superfície de gestão de claims permitisse a um admin
        // de empresa auto-invocar as claims de impersonation: ele continuaria sem poder "voltar"
        // para o super admin.
        if (!TemClaimEmpresa(principalAtual))
        {
            return VoltarCentralResult.Failure(MensagemSemImpersonacao);
        }

        var idOriginal = principalAtual.FindFirst(TenantContext.ClaimSuperAdminOriginalId)?.Value;
        if (!Guid.TryParse(idOriginal, out var superAdminId) || superAdminId == Guid.Empty)
        {
            return VoltarCentralResult.Failure(MensagemSemImpersonacao);
        }

        var idAtual = principalAtual.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(idAtual, out var usuarioAtualId) || usuarioAtualId == superAdminId)
        {
            // Sem identificador válido não há como provar quem é o principal impersonado; e um
            // super admin "impersonando a si mesmo" não é uma impersonation legítima.
            return VoltarCentralResult.Failure(MensagemSemImpersonacao);
        }

        var superAdmin = await userManager.FindByIdAsync(superAdminId.ToString());
        if (superAdmin is null
            || !superAdmin.Ativo
            || superAdmin.EmpresaId is not null
            || !await userManager.IsInRoleAsync(superAdmin, ApplicationRole.SuperAdmin))
        {
            // O super admin original deixou de ser válido (inativado, virou usuário de empresa ou
            // perdeu a role): fail-closed, não reemite nada.
            return VoltarCentralResult.Failure(
                "A sessão do super administrador não é mais válida. Faça login novamente.");
        }

        // Dados da empresa que está sendo deixada, para o detalhe da auditoria. Lidos ANTES do sign-in
        // porque depois dele o DbContext scoped continua no tenant da empresa impersonada — e o registro
        // precisa ser legível por quem audita a central.
        var empresaId = Guid.Parse(principalAtual.FindFirst(TenantContext.ClaimEmpresaId)!.Value);
        var empresa = await db.Set<Empresa>().AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == empresaId, cancellationToken);

        // Principal novo a partir da factory (sem as claims de impersonation), como num login normal.
        var principal = await principalDoUsuario.CriarAsync(superAdmin);

        // Cookie reemitido no esquema do Identity (o principal já veio pronto da factory, sem as
        // claims de impersonation).
        await httpContextAccessor.HttpContext!.SignInAsync(
            IdentityConstants.ApplicationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = false });

        // Auditoria DEPOIS do sign-in bem-sucedido (mesma ordem do "Entrar como"): se a troca de
        // cookie falhasse, a volta não teria acontecido e registrar seria mentira. Assina o SUPER
        // ADMIN — quem voltou —, nunca o admin impersonado. Sem este registro a trilha mostra a
        // entrada numa empresa e nunca a saída.
        //
        // Autor NÃO vem do AuthenticationStateProvider: este handler roda num endpoint HTTP
        // (POST /central/voltar), onde não há circuito Blazor e o provider scoped não existe —
        // pedi-lo aqui derrubava a requisição com 500. O principal da própria requisição é a
        // fonte correta, e o super admin original já foi validado acima (ativo, sem empresa,
        // role SuperAdmin).
        var detalheEmpresa = empresa is null
            ? $"a empresa {empresaId}"
            : $"a empresa '{empresa.Nome}'";
        // Quem estava sendo impersonado: sem isto o registro diz "saiu de uma empresa", mas não
        // de qual conta — e o par EntrarComo/VoltarCentral deixa de ser reconciliável.
        var adminImpersonado = principalAtual.Identity?.Name;

        db.RegistrosAuditoria.Add(new RegistroAuditoria(
            empresaId,
            superAdmin.Id,
            superAdmin.Email ?? superAdmin.UserName ?? UsuarioAtual.Sistema,
            RegistroAuditoria.Acoes.VoltarCentral,
            adminImpersonado is null
                ? $"Super admin encerrou a impersonation e voltou à central, deixando {detalheEmpresa}."
                : $"Super admin encerrou a impersonation de {adminImpersonado} e voltou à central, deixando {detalheEmpresa}.",
            RegistroAuditoria.Origens.Central));

        // Save só da auditoria (esta operação não altera domínio). Falha propaga (fail loud): o
        // cookie já foi emitido, mas a ação não pode ficar sem trilha.
        await db.SaveChangesAsync(cancellationToken);

        return VoltarCentralResult.Success();
    }

    private static bool LerBooleano(ClaimsPrincipal principal, string tipoClaim) =>
        bool.TryParse(principal.FindFirst(tipoClaim)?.Value, out var valor) && valor;

    private static bool TemClaimEmpresa(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst(TenantContext.ClaimEmpresaId)?.Value, out var empresaId)
        && empresaId != Guid.Empty;
}