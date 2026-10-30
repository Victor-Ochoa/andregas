using AndreGas.Infrastructure.Identity;
using AndreGas.Web.Common.Tenancy;

using Microsoft.AspNetCore.Authorization;

namespace AndreGas.Web.Common.Authorization;

/// <summary>
/// Políticas de autorização declarativas usadas nas rotas Blazor ([Authorize(Policy=...)]),
/// controllers e componentes. Concentrar aqui evita repetir a lógica de role em cada página.
/// </summary>
public static class Politicas
{
    public const string ApenasAdmin = nameof(ApenasAdmin);

    /// <summary>
    /// Somente o super administrador da central. Usada na área da central
    /// (<c>/central</c> e o POST de "Entrar como"). O principal impersonado <b>não</b> tem esta
    /// role — por isso o "Voltar à central" não pode usá-la (ver decisão 5 da Task 9).
    /// </summary>
    public const string ApenasSuperAdmin = nameof(ApenasSuperAdmin);

    /// <summary>
    /// Somente usuários vinculados a uma empresa (claim <see cref="TenantContext.ClaimEmpresaId"/>).
    /// Aplicada às páginas de domínio: o super admin da central (sem empresa) não pode cair numa
    /// delas e estourar <c>EmpresaIdRequerida</c> com 500 — ele é redirecionado para
    /// <c>/central</c> pelo <c>RoleAwareNotAuthorized</c>.
    /// </summary>
    public const string ApenasEmpresa = nameof(ApenasEmpresa);

    public static void AdicionarPoliticas(AuthorizationOptions options)
    {
        // Toda rota exige usuário autenticado por padrão (FallbackPolicy já definida em Program.cs);
        // esta política restringe ainda mais: somente usuários com a role Admin.
        options.AddPolicy(ApenasAdmin, policy =>
            policy.RequireAuthenticatedUser().RequireRole(ApplicationRole.Admin));

        options.AddPolicy(ApenasSuperAdmin, policy =>
            policy.RequireAuthenticatedUser().RequireRole(ApplicationRole.SuperAdmin));

        // A claim EmpresaId é emitida pela factory para TODO usuário com ApplicationUser.EmpresaId
        // definido (ver ApplicationUserClaimsPrincipalFactory). O super admin da central não a
        // possui, então é barrado aqui.
        options.AddPolicy(ApenasEmpresa, policy =>
            policy.RequireAuthenticatedUser().RequireClaim(TenantContext.ClaimEmpresaId));
    }
}