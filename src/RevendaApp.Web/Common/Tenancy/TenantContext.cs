using System.Security.Claims;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;

using RevendaApp.Infrastructure.Tenancy;

namespace RevendaApp.Web.Common.Tenancy;

/// <summary>
/// Implementação scoped de <see cref="ITenantContext"/> que lê a empresa (tenant) das claims do
/// principal autenticado via <see cref="AuthenticationStateProvider"/> do circuito Blazor.
/// </summary>
/// <remarks>
/// <para>
/// As claims (<see cref="ClaimEmpresaId"/>, <see cref="ClaimEmpresaNome"/>, <see cref="ClaimEmpresaCor"/>,
/// <see cref="ClaimEhSuperAdmin"/>, <see cref="ClaimImpersonando"/>) são adicionadas ao principal no
/// login por um <c>IUserClaimsPrincipalFactory&lt;ApplicationUser&gt;</c> customizado (Task 3).
/// </para>
/// <para>
/// <b>Fonte do principal:</b> o principal da requisição HTTP
/// (<see cref="IHttpContextAccessor"/>) tem precedência; o <see cref="AuthenticationStateProvider"/>
/// (circuito interativo do Blazor) é o fallback. Motivo: durante o prerender SSR o contexto pode ser
/// resolvido fora do escopo de um componente Razor (ex.: dentro do <c>SecurityStampValidator</c> da
/// autenticação por cookie), quando o <see cref="AuthenticationStateProvider"/> não está disponível —
/// mas o <c>HttpContext.User</c> já está autenticado. Consultar só o provider nesse momento faria o
/// tenant cair em "modo central" e vazar dados entre empresas.
/// </para>
/// <para>
/// <b>Regra de segurança (fail-closed):</b> só se considera "modo central" (enxerga todas as
/// empresas) em dois casos explícitos — (a) <b>não autenticado</b> (seed na inicialização, SSR
/// estático antes do login, design-time) ou (b) autenticado com a claim
/// <see cref="ClaimEhSuperAdmin"/> = <c>true</c>. Um usuário <b>autenticado</b>, que <b>não</b> é
/// super admin e <b>sem</b> a claim <see cref="ClaimEmpresaId"/>, é uma configuração inválida:
/// <see cref="EmpresaId"/> lança em vez de conceder leitura de todas as empresas.
/// </para>
/// <para>
/// A única degradação tolerada para "não autenticado" é a condição <b>conhecida</b> do
/// <c>ServerAuthenticationStateProvider</c> quando não há circuito/escopo Razor (mensagem
/// "outside of the DI scope for a Razor component"). Qualquer <b>outra</b> exceção do provider
/// propaga (fail loud) — nunca vira acesso a todos os tenants.
/// </para>
/// </remarks>
public sealed class TenantContext(
    AuthenticationStateProvider authStateProvider,
    IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    /// <summary>Claim com o Id (Guid) da empresa do usuário. Ausente = central (super admin).</summary>
    public const string ClaimEmpresaId = "EmpresaId";

    /// <summary>Claim com o nome da empresa para exibição no app bar.</summary>
    public const string ClaimEmpresaNome = "EmpresaNome";

    /// <summary>Claim com a cor primária (hex) da empresa.</summary>
    public const string ClaimEmpresaCor = "EmpresaCor";

    /// <summary>Claim booleana ("true"/"false") indicando o super admin da central.</summary>
    public const string ClaimEhSuperAdmin = "EhSuperAdmin";

    /// <summary>Claim booleana ("true"/"false") indicando impersonation de uma empresa.</summary>
    public const string ClaimImpersonando = "Impersonando";

    /// <summary>
    /// Claim com o Id (Guid) do super admin que iniciou a impersonation. É a <b>única</b> fonte
    /// aceita para o "Voltar à central": o endpoint valida este Guid contra o banco (usuário que
    /// ainda é super admin ativo e sem empresa) — nunca um id vindo do corpo/query, que seria
    /// forjável. Ausente fora de uma sessão impersonada.
    /// </summary>
    public const string ClaimSuperAdminOriginalId = "SuperAdminOriginalId";

    /// <summary>
    /// Claim com o instante (UTC, formato "O"/round-trip) em que a impersonation <b>começou</b>.
    /// É a base do <b>teto absoluto</b> da sessão impersonada: o cookie do Identity renova sozinho
    /// (sliding + <c>SecurityStampValidator</c>), então a única forma de limitar a duração total é
    /// uma marca imutável no principal — ver
    /// <see cref="RevendaApp.Web.Common.Authorization.ValidadeAbsolutaImpersonacao"/>. Ausente fora de
    /// uma sessão impersonada.
    /// </summary>
    public const string ClaimImpersonacaoIniciadaEm = "ImpersonacaoIniciadaEm";

    /// <summary>
    /// Trecho exato da mensagem lançada pelo <c>ServerAuthenticationStateProvider</c> quando não há
    /// circuito/escopo Razor (seed na inicialização, SSR estático). Só essa condição degrada para
    /// "não autenticado"; qualquer outra exceção propaga.
    /// </summary>
    private const string TrechoMensagemForaDeCircuito = "outside of the DI scope for a Razor component";

    private static readonly ClaimsPrincipal Anonimo = new(new ClaimsIdentity());

    public Guid? EmpresaId
    {
        get
        {
            var principal = ObterPrincipal();

            // (a) Não autenticado → modo central (seed/reset/design-time). O design prevê que o
            //     seed enxergue e grave todas as empresas.
            if (principal.Identity is not { IsAuthenticated: true })
            {
                return null;
            }

            var valor = principal.FindFirst(ClaimEmpresaId)?.Value;
            if (Guid.TryParse(valor, out var id) && id != Guid.Empty)
            {
                return id;
            }

            // (b) Autenticado sem empresa: somente o super admin da central pode operar sem
            //     EmpresaId (e então enxerga tudo). Qualquer outro caso é configuração inválida:
            //     falha explícita em vez de "fail open" para leitura de todas as empresas.
            if (TemClaimSuperAdmin(principal))
            {
                return null;
            }

            throw new InvalidOperationException(
                "Usuário autenticado sem empresa (claim 'EmpresaId' ausente) e que não é super admin da central. "
                + "Acesso a dados de domínio negado.");
        }
    }

    public string NomeEmpresa => ObterPrincipal().FindFirst(ClaimEmpresaNome)?.Value ?? string.Empty;

    public string CorPrimaria => ObterPrincipal().FindFirst(ClaimEmpresaCor)?.Value ?? string.Empty;

    public bool EhSuperAdmin => TemClaimSuperAdmin(ObterPrincipal());

    public bool Impersonando => LerBooleano(ClaimImpersonando);

    public Guid EmpresaIdRequerida(string acao)
    {
        return EmpresaId
            ?? throw new InvalidOperationException(
                $"A operação '{acao}' exige uma empresa selecionada. Selecione/entre em uma empresa para continuar.");
    }

    private ClaimsPrincipal ObterPrincipal()
    {
        // (1) Principal da requisição HTTP tem precedência: cobre prerender SSR e qualquer
        //     resolução fora do circuito (ex.: SecurityStampValidator durante a autenticação por
        //     cookie), onde o AuthenticationStateProvider ainda não existe. Só um principal
        //     AUTENTICADO é aceito aqui — um HttpContext.User anônimo (request não autenticado)
        //     não pode mascarar um circuito interativo já autenticado, então cai no fallback.
        var principalHttp = httpContextAccessor.HttpContext?.User;
        if (principalHttp?.Identity is { IsAuthenticated: true })
        {
            return principalHttp;
        }

        // (2) Fallback: circuito interativo do Blazor.
        try
        {
            // GetAuthenticationStateAsync devolve uma Task já concluída nos providers do Blazor
            // (o estado é materializado ao iniciar o circuito/request), então não há bloqueio.
            // ITenantContext é síncrono por contrato (usado no filtro do AppDbContext e no
            // interceptor de SaveChanges, ambos síncronos).
            return authStateProvider.GetAuthenticationStateAsync().GetAwaiter().GetResult().User;
        }
        catch (InvalidOperationException ex)
            when (ex.Message.Contains(TrechoMensagemForaDeCircuito, StringComparison.Ordinal))
        {
            // Condição CONHECIDA: provider sem circuito/escopo Razor (seed na inicialização,
            // SSR estático, design-time). Trata como não autenticado (modo central do seed).
            // Qualquer outra exceção NÃO é capturada aqui — propaga para não ampliar acesso.
            return Anonimo;
        }
    }

    private static bool TemClaimSuperAdmin(ClaimsPrincipal principal) =>
        bool.TryParse(principal.FindFirst(ClaimEhSuperAdmin)?.Value, out var valor) && valor;

    private bool LerBooleano(string claimType) =>
        bool.TryParse(ObterPrincipal().FindFirst(claimType)?.Value, out var valor) && valor;
}