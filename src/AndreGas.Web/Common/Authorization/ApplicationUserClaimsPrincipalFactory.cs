using System.Security.Claims;

using AndreGas.Domain.Entities;
using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Identity;
using AndreGas.Web.Common.Tenancy;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AndreGas.Web.Common.Authorization;

/// <summary>
/// Fábrica do <see cref="ClaimsPrincipal"/> do Identity que acrescenta as claims de multi-empresa
/// (<see cref="TenantContext.ClaimEmpresaId"/>, <see cref="TenantContext.ClaimEmpresaNome"/>,
/// <see cref="TenantContext.ClaimEmpresaCor"/> e <see cref="TenantContext.ClaimEhSuperAdmin"/>),
/// lidas depois pelo <see cref="TenantContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Invariante de tenancy (crítica):</b> todo usuário com <c>ApplicationUser.EmpresaId</c>
/// definido <b>precisa</b> receber a claim <see cref="TenantContext.ClaimEmpresaId"/> — sem
/// exceção. O <see cref="TenantContext"/> é <i>fail-closed</i>: um usuário autenticado, sem essa
/// claim e que não seja super admin, tem o acesso a dados de domínio negado por exceção. Por isso
/// a claim é emitida mesmo quando a linha da empresa não é encontrada (nesse caso apenas nome/cor
/// ficam de fora): omiti-la faria o filtro global do <c>AppDbContext</c> "alargar" para todas as
/// empresas. A empresa inativa é barrada antes, no <c>LoginCommandHandler</c>.
/// </para>
/// <para>
/// A empresa é lida do <see cref="AppDbContext"/> (serviço scoped, portanto seguro de injetar
/// aqui) porque as claims só carregam valores simples — o nome e a cor não estão no
/// <see cref="ApplicationUser"/>. A tabela <c>Empresas</c> não tem filtro por empresa, então a
/// leitura funciona igual nos modos central e de empresa.
/// </para>
/// </remarks>
public sealed class ApplicationUserClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IOptions<IdentityOptions> options,
    AppDbContext db)
    : UserClaimsPrincipalFactory<ApplicationUser, ApplicationRole>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        // Sempre emitida ("true"/"false"): o TenantContext só concede modo central quando esta
        // claim é explicitamente "true".
        var ehSuperAdmin = await UserManager.IsInRoleAsync(user, ApplicationRole.SuperAdmin);
        identity.AddClaim(new Claim(TenantContext.ClaimEhSuperAdmin, ehSuperAdmin ? "true" : "false"));

        if (user.EmpresaId is not { } empresaId)
        {
            // Central: sem claim de empresa (só o super admin pode operar assim — o TenantContext
            // valida isso e nega o acesso de qualquer outro usuário nessa situação).
            return identity;
        }

        // Emitida SEMPRE, antes de qualquer consulta: é a claim que fecha o tenant.
        identity.AddClaim(new Claim(TenantContext.ClaimEmpresaId, empresaId.ToString()));

        var empresa = await db.Set<Empresa>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == empresaId);

        if (empresa is null)
        {
            // Empresa inexistente: mantém a claim de Id (não alarga o tenant) e segue sem nome/cor.
            return identity;
        }

        identity.AddClaim(new Claim(TenantContext.ClaimEmpresaNome, empresa.Nome));

        if (!string.IsNullOrWhiteSpace(empresa.CorPrimaria))
        {
            identity.AddClaim(new Claim(TenantContext.ClaimEmpresaCor, empresa.CorPrimaria));
        }

        return identity;
    }
}