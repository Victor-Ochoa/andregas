using System.Security.Claims;

using AndreGas.Web.Common.Tenancy;

using Microsoft.AspNetCore.Identity;

namespace AndreGas.Web.Common.Authorization;

/// <summary>
/// Callback de renovação de principal do <c>SecurityStampValidator</c> que <b>preserva</b> as
/// claims de impersonation (<see cref="TenantContext.ClaimImpersonando"/> e
/// <see cref="TenantContext.ClaimSuperAdminOriginalId"/>) quando o Identity reemite o principal.
/// </summary>
/// <remarks>
/// <para>
/// <b>Problema que resolve:</b> a cada <c>SecurityStampValidatorOptions.ValidationInterval</c>
/// (padrão 30 minutos) o Identity relê o usuário do <c>NameIdentifier</c> do principal e
/// <b>substitui</b> o principal por um novo, montado pela
/// <see cref="ApplicationUserClaimsPrincipalFactory"/>. Numa sessão impersonada o
/// <c>NameIdentifier</c> é o <b>admin da empresa</b>, então o novo principal perde as claims de
/// impersonation. Consequências observadas antes desta correção: o badge "Entrando como" some, o
/// "Voltar à central" falha (a claim <c>SuperAdminOriginalId</c> não existe mais), a sessão vira um
/// login permanente e sem badge do admin da empresa e o registro de quem estava agindo se perde.
/// </para>
/// <para>
/// <b>Fail-closed:</b> só reaplica quando o principal atual traz
/// <see cref="TenantContext.ClaimImpersonando"/> = <c>"true"</c> <b>e</b> um
/// <see cref="TenantContext.ClaimSuperAdminOriginalId"/> que seja um <see cref="Guid"/> não vazio.
/// Qualquer outra combinação não propaga nada (não cria impersonation "fantasma" sem caminho de
/// volta). Claims já presentes no novo principal não são duplicadas.
/// </para>
/// <para>
/// <b>Por que não basta "não validar":</b> desligar o <c>SecurityStampValidator</c> inteiro
/// removeria a invalidação de sessão em troca de senha/inativação — o callback preserva a
/// segurança e apenas reanexa as claims de auditoria.
/// </para>
/// </remarks>
public static class RenovacaoPrincipalImpersonacao
{
    /// <summary>
    /// Reaplica as claims de impersonation do <see cref="SecurityStampRefreshingPrincipalContext.CurrentPrincipal"/>
    /// no <see cref="SecurityStampRefreshingPrincipalContext.NewPrincipal"/>, quando presentes e válidas.
    /// </summary>
    public static Task ReaplicarClaimsAsync(SecurityStampRefreshingPrincipalContext context)
    {
        Reaplicar(context.CurrentPrincipal, context.NewPrincipal);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Reaplica as claims de impersonation de <paramref name="atual"/> em <paramref name="novo"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Reuso obrigatório:</b> o mesmo perigo do <c>SecurityStampValidator</c> existe em qualquer
    /// outro caminho que reemita o principal pela factory — hoje o <c>POST
    /// /configuracoes/atualizar-sessao</c>, que monta o principal com
    /// <c>CreateUserPrincipalAsync</c> + <c>SignInAsync</c>. Se um deles
    /// esquecer de chamar isto, o super admin impersonado perde <see cref="TenantContext.ClaimImpersonando"/>
    /// e <see cref="TenantContext.ClaimSuperAdminOriginalId"/>: o badge "Entrando como" some, o
    /// "Voltar à central" falha para sempre e ele fica preso na sessão da empresa.
    /// </para>
    /// <para>
    /// <b>Fail-closed:</b> só reaplica quando o principal atual traz
    /// <see cref="TenantContext.ClaimImpersonando"/> = <c>"true"</c> <b>e</b> um
    /// <see cref="TenantContext.ClaimSuperAdminOriginalId"/> que seja um <see cref="Guid"/> não
    /// vazio. Claims já presentes no novo principal não são duplicadas.
    /// </para>
    /// </remarks>
    /// <returns><c>true</c> quando as claims foram reaplicadas; <c>false</c> nos demais casos.</returns>
    public static bool Reaplicar(ClaimsPrincipal? atual, ClaimsPrincipal? novo)
    {
        if (atual is null || novo is null || !EstaImpersonando(atual))
        {
            return false;
        }

        var idOriginal = atual.FindFirst(TenantContext.ClaimSuperAdminOriginalId)?.Value;
        if (!Guid.TryParse(idOriginal, out var superAdminId) || superAdminId == Guid.Empty)
        {
            return false;
        }

        var identity = novo.Identities.FirstOrDefault();
        if (identity is null)
        {
            return false;
        }

        if (novo.FindFirst(TenantContext.ClaimImpersonando) is null)
        {
            identity.AddClaim(new Claim(TenantContext.ClaimImpersonando, "true"));
        }

        if (novo.FindFirst(TenantContext.ClaimSuperAdminOriginalId) is null)
        {
            identity.AddClaim(new Claim(TenantContext.ClaimSuperAdminOriginalId, superAdminId.ToString()));
        }

        return true;
    }

    /// <summary>Cria o callback pronto para o <see cref="SecurityStampValidatorOptions"/>.</summary>
    public static Func<SecurityStampRefreshingPrincipalContext, Task> CriarCallback() => ReaplicarClaimsAsync;

    /// <summary>
    /// Configura o <see cref="SecurityStampValidatorOptions"/> para preservar as claims de
    /// impersonation na renovação. Chamado por <c>Program.cs</c>; isolado aqui para que um teste
    /// possa verificar a <b>ligação</b> (não só o callback), que é justamente o que se perde se
    /// alguém remover o registro.
    /// </summary>
    public static void Configurar(SecurityStampValidatorOptions options) =>
        options.OnRefreshingPrincipal = CriarCallback();

    private static bool EstaImpersonando(ClaimsPrincipal principal) =>
        bool.TryParse(principal.FindFirst(TenantContext.ClaimImpersonando)?.Value, out var valor) && valor;
}