using System.Security.Claims;

using AndreGas.Web.Common.Authorization;
using AndreGas.Web.Common.Tenancy;

using Microsoft.AspNetCore.Identity;

namespace AndreGas.UnitTests.Common.Authorization;

/// <summary>
/// Testes do callback de renovação de principal do <c>SecurityStampValidator</c>.
/// </summary>
/// <remarks>
/// <b>Por que isto importa:</b> o Identity revalida o <i>security stamp</i> a cada
/// <c>ValidationInterval</c> (padrão 30 min) e <b>substitui</b> o principal da sessão por um novo
/// montado pela factory a partir do usuário do <c>NameIdentifier</c> — que, numa impersonation, é
/// o <b>admin da empresa</b>. Sem reaplicar as claims, o badge "Entrando como" desaparece, o
/// "Voltar à central" deixa de funcionar (a claim <c>SuperAdminOriginalId</c> some) e a sessão
/// vira um login permanente e sem registro de quem estava agindo.
/// </remarks>
public class RenovacaoPrincipalImpersonacaoTests
{
    private static ClaimsPrincipal CriarPrincipal(params (string Tipo, string Valor)[] claims)
    {
        var identity = new ClaimsIdentity(claims.Select(c => new Claim(c.Tipo, c.Valor)), "Identity.Application");
        return new ClaimsPrincipal(identity);
    }

    private static string? Valor(ClaimsPrincipal principal, string tipo) =>
        principal.FindFirst(tipo)?.Value;

    [Fact]
    public async Task Renovacao_reaplica_claims_de_impersonation_no_novo_principal()
    {
        var idSuperAdmin = Guid.NewGuid();
        var current = CriarPrincipal(
            (ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            (TenantContext.ClaimEmpresaId, Guid.NewGuid().ToString()),
            (TenantContext.ClaimImpersonando, "true"),
            (TenantContext.ClaimSuperAdminOriginalId, idSuperAdmin.ToString()));

        // O novo principal vem "limpo" da factory (só as claims do admin da empresa).
        var novo = CriarPrincipal(
            (ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            (TenantContext.ClaimEmpresaId, Guid.NewGuid().ToString()),
            (TenantContext.ClaimEhSuperAdmin, "false"));

        var contexto = new SecurityStampRefreshingPrincipalContext { CurrentPrincipal = current, NewPrincipal = novo };

        await RenovacaoPrincipalImpersonacao.ReaplicarClaimsAsync(contexto);

        Assert.Equal("true", Valor(contexto.NewPrincipal!, TenantContext.ClaimImpersonando));
        Assert.Equal(idSuperAdmin.ToString(), Valor(contexto.NewPrincipal!, TenantContext.ClaimSuperAdminOriginalId));
    }

    [Fact]
    public async Task Renovacao_nao_adiciona_claims_quando_a_sessao_nao_e_impersonada()
    {
        var current = CriarPrincipal(
            (ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            (TenantContext.ClaimEhSuperAdmin, "false"));

        var novo = CriarPrincipal((ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));

        var contexto = new SecurityStampRefreshingPrincipalContext { CurrentPrincipal = current, NewPrincipal = novo };

        await RenovacaoPrincipalImpersonacao.ReaplicarClaimsAsync(contexto);

        Assert.Null(Valor(contexto.NewPrincipal!, TenantContext.ClaimImpersonando));
        Assert.Null(Valor(contexto.NewPrincipal!, TenantContext.ClaimSuperAdminOriginalId));
    }

    [Fact]
    public async Task Renovacao_ignora_impersonando_sem_id_original_valido()
    {
        // Claim Impersonando sem um Guid válido de origem não é uma impersonation legítima:
        // não se propaga nada (fail-closed) — evita "impersonation fantasma" sem como voltar.
        var current = CriarPrincipal(
            (ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            (TenantContext.ClaimImpersonando, "true"),
            (TenantContext.ClaimSuperAdminOriginalId, "nao-e-um-guid"));

        var novo = CriarPrincipal((ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));

        var contexto = new SecurityStampRefreshingPrincipalContext { CurrentPrincipal = current, NewPrincipal = novo };

        await RenovacaoPrincipalImpersonacao.ReaplicarClaimsAsync(contexto);

        Assert.Null(Valor(contexto.NewPrincipal!, TenantContext.ClaimImpersonando));
        Assert.Null(Valor(contexto.NewPrincipal!, TenantContext.ClaimSuperAdminOriginalId));
    }

    [Fact]
    public async Task Configurar_liga_o_callback_no_SecurityStampValidatorOptions()
    {
        // Prova a LIGAÇÃO usada por Program.cs: sem ela (alguém removendo o
        // builder.Services.Configure<SecurityStampValidatorOptions>(...)), o Identity volta ao
        // comportamento padrão e as claims somem na primeira renovação.
        var idSuperAdmin = Guid.NewGuid();
        var options = new SecurityStampValidatorOptions();

        RenovacaoPrincipalImpersonacao.Configurar(options);

        Assert.NotNull(options.OnRefreshingPrincipal);

        var contexto = new SecurityStampRefreshingPrincipalContext
        {
            CurrentPrincipal = CriarPrincipal(
                (TenantContext.ClaimImpersonando, "true"),
                (TenantContext.ClaimSuperAdminOriginalId, idSuperAdmin.ToString())),
            NewPrincipal = CriarPrincipal((ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())),
        };

        await options.OnRefreshingPrincipal(contexto);

        Assert.Equal("true", Valor(contexto.NewPrincipal!, TenantContext.ClaimImpersonando));
        Assert.Equal(idSuperAdmin.ToString(), Valor(contexto.NewPrincipal!, TenantContext.ClaimSuperAdminOriginalId));
    }

    [Fact]
    public async Task Renovacao_nao_duplica_claims_ja_presentes_no_novo_principal()
    {
        var idSuperAdmin = Guid.NewGuid();
        var current = CriarPrincipal(
            (TenantContext.ClaimImpersonando, "true"),
            (TenantContext.ClaimSuperAdminOriginalId, idSuperAdmin.ToString()));

        var novo = CriarPrincipal(
            (TenantContext.ClaimImpersonando, "true"),
            (TenantContext.ClaimSuperAdminOriginalId, idSuperAdmin.ToString()));

        var contexto = new SecurityStampRefreshingPrincipalContext { CurrentPrincipal = current, NewPrincipal = novo };

        await RenovacaoPrincipalImpersonacao.ReaplicarClaimsAsync(contexto);

        Assert.Single(contexto.NewPrincipal!.FindAll(TenantContext.ClaimImpersonando));
        Assert.Single(contexto.NewPrincipal!.FindAll(TenantContext.ClaimSuperAdminOriginalId));
    }
}