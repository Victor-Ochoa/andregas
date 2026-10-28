using System.Security.Claims;

using AndreGas.Infrastructure.Tenancy;

using Microsoft.AspNetCore.Components.Authorization;

namespace AndreGas.Web.Common.Tenancy;

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
/// A leitura é <b>defensiva</b>: quando o <see cref="AuthenticationStateProvider"/> ainda não está
/// pronto (SSR estático da página de login, seed na inicialização, design-time) ou quando as claims
/// estão ausentes, devolve os padrões seguros — <c>null</c>/vazio/<c>false</c> (modo central), sem
/// lançar. Isso mantém login e seed funcionando antes de existir um usuário com empresa.
/// </para>
/// <para>
/// O principal é lido a cada acesso (não cacheado): o custo é desprezível (o provider devolve um
/// estado já materializado) e evita "congelar" um tenant obsoleto dentro do escopo/circuito.
/// </para>
/// </remarks>
public sealed class TenantContext(AuthenticationStateProvider authStateProvider) : ITenantContext
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

    private static readonly ClaimsPrincipal Anonimo = new(new ClaimsIdentity());

    public Guid? EmpresaId
    {
        get
        {
            var valor = ObterPrincipal().FindFirst(ClaimEmpresaId)?.Value;
            return Guid.TryParse(valor, out var id) && id != Guid.Empty ? id : null;
        }
    }

    public string NomeEmpresa => ObterPrincipal().FindFirst(ClaimEmpresaNome)?.Value ?? string.Empty;

    public string CorPrimaria => ObterPrincipal().FindFirst(ClaimEmpresaCor)?.Value ?? string.Empty;

    public bool EhSuperAdmin => LerBooleano(ClaimEhSuperAdmin);

    public bool Impersonando => LerBooleano(ClaimImpersonando);

    public Guid EmpresaIdRequerida(string acao)
    {
        return EmpresaId
            ?? throw new InvalidOperationException(
                $"A operação '{acao}' exige uma empresa selecionada. Selecione/entre em uma empresa para continuar.");
    }

    private ClaimsPrincipal ObterPrincipal()
    {
        try
        {
            // GetAuthenticationStateAsync devolve uma Task já concluída nos providers do Blazor
            // (o estado é materializado ao iniciar o circuito/request), então não há bloqueio.
            // ITenantContext é síncrono por contrato (usado no filtro do AppDbContext e no
            // interceptor de SaveChanges, ambos síncronos).
            return authStateProvider.GetAuthenticationStateAsync().GetAwaiter().GetResult().User;
        }
        catch (InvalidOperationException)
        {
            // Provider indisponível neste contexto (ex.: SSR estático sem circuito, seed,
            // design-time): trata como anônimo/central em vez de derrubar a operação.
            return Anonimo;
        }
    }

    private bool LerBooleano(string claimType) =>
        bool.TryParse(ObterPrincipal().FindFirst(claimType)?.Value, out var valor) && valor;
}