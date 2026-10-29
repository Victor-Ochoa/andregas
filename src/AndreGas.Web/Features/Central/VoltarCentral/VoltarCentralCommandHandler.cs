using System.Security.Claims;

using AndreGas.Infrastructure.Identity;
using AndreGas.Web.Common.Authorization;
using AndreGas.Web.Common.Tenancy;

using Mediator;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace AndreGas.Web.Features.Central.VoltarCentral;

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
/// <see cref="TenantContext.ClaimImpersonando"/> for <c>"true"</c> e (b) o Guid da claim apontar
/// para um usuário que <b>ainda</b> é super admin, <b>ativo</b> e <b>sem empresa</b>. Qualquer
/// outra situação devolve falha — o endpoint responde 403/redirect para <c>/sem-autorizacao</c>.
/// </para>
/// </remarks>
public sealed class VoltarCentralCommandHandler(
    IHttpContextAccessor httpContextAccessor,
    UserManager<ApplicationUser> userManager,
    IPrincipalDoUsuario principalDoUsuario)
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

        var idOriginal = principalAtual.FindFirst(TenantContext.ClaimSuperAdminOriginalId)?.Value;
        if (!Guid.TryParse(idOriginal, out var superAdminId) || superAdminId == Guid.Empty)
        {
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

        // Principal novo a partir da factory (sem as claims de impersonation), como num login normal.
        var principal = await principalDoUsuario.CriarAsync(superAdmin);

        // Cookie reemitido no esquema do Identity (o principal já veio pronto da factory, sem as
        // claims de impersonation).
        await httpContextAccessor.HttpContext!.SignInAsync(
            IdentityConstants.ApplicationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = false });

        return VoltarCentralResult.Success();
    }

    private static bool LerBooleano(ClaimsPrincipal principal, string tipoClaim) =>
        bool.TryParse(principal.FindFirst(tipoClaim)?.Value, out var valor) && valor;
}