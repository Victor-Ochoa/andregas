using System.Globalization;
using System.Security.Claims;

using AndreGas.Domain.Entities;
using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Identity;
using AndreGas.Web.Common.Authorization;
using AndreGas.Web.Common.Tenancy;

using Mediator;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Central.EntrarComo;

/// <summary>
/// Executa o "Entrar como": valida que quem pede é o super admin da central (e que não está já
/// impersonando), resolve o admin da empresa alvo e reemite o cookie com o principal desse admin,
/// acrescentando as claims <see cref="TenantContext.ClaimImpersonando"/> e
/// <see cref="TenantContext.ClaimSuperAdminOriginalId"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Escolha do admin (determinística):</b> se a empresa tiver mais de um usuário ativo na role
/// <c>Admin</c>, entra-se pelo de <b>menor <see cref="ApplicationUser.Id"/></b> (ordenado por Id).
/// O critério é arbitrário — esta task entra sempre pela empresa, não escolhe usuário — mas
/// determinístico, para que o comportamento seja reproduzível e testável.
/// </para>
/// <para>
/// <b>Requisição de página completa:</b> o endpoint que chama este handler responde com redirect;
/// o cliente precisa navegar com <c>forceLoad: true</c>. O circuito Blazor em andamento mantém o
/// principal e o <c>AppDbContext</c> da sessão anterior (requisito duro da Task 2).
/// </para>
/// </remarks>
public sealed class EntrarComoCommandHandler(
    IHttpContextAccessor httpContextAccessor,
    UserManager<ApplicationUser> userManager,
    AppDbContext db,
    IPrincipalDoUsuario principalDoUsuario,
    TimeProvider timeProvider)
    : ICommandHandler<EntrarComoCommand, EntrarComoResult>
{
    /// <summary>
    /// Validade absoluta do cookie de <b>impersonation</b>. Escolhido menor que a vida útil normal
    /// de uma sessão de operador (o cookie do Identity com <c>IsPersistent=false</c> vive enquanto
    /// a sessão do navegador viver, e o <c>SecurityStampValidator</c> só revalida a cada 30 min):
    /// 8 horas cobrem um turno de trabalho sem deixar um "modo empresa" aberto indefinidamente —
    /// lembre-se de que <c>/central/voltar</c> troca esse ticket por um principal de super admin
    /// pleno. O operador simplesmente repete o "Entrar como" se a janela expirar.
    /// </summary>
    public static readonly TimeSpan ValidadeImpersonacao = TimeSpan.FromHours(8);

    public async ValueTask<EntrarComoResult> Handle(EntrarComoCommand command, CancellationToken cancellationToken)
    {
        var principalAtual = httpContextAccessor.HttpContext?.User
            ?? throw new InvalidOperationException(
                "Não foi possível identificar o usuário autenticado para executar o 'Entrar como'.");

        // Limite real de segurança (a política da rota é apenas a primeira barreira): só o super
        // admin da central entra como empresa, e não se impersona de dentro de uma impersonation.
        if (!LerBooleano(principalAtual, TenantContext.ClaimEhSuperAdmin))
        {
            throw new InvalidOperationException("Somente o super administrador da central pode entrar em uma empresa.");
        }

        if (LerBooleano(principalAtual, TenantContext.ClaimImpersonando))
        {
            throw new InvalidOperationException("Não é possível entrar em outra empresa a partir de uma sessão já impersonada.");
        }

        var superAdminId = ObterIdUsuario(principalAtual);

        var empresa = await db.Set<Empresa>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == command.EmpresaId, cancellationToken);

        if (empresa is null)
        {
            return EntrarComoResult.Failure("Empresa não encontrada.", EntrarComoResult.Motivos.EmpresaNaoEncontrada);
        }

        if (!empresa.Ativo)
        {
            return EntrarComoResult.Failure(
                "Empresa desativada. Reative a empresa antes de entrar nela.",
                EntrarComoResult.Motivos.EmpresaInativa);
        }

        var admin = await ObterAdminDaEmpresaAsync(command.EmpresaId);
        if (admin is null)
        {
            return EntrarComoResult.Failure(
                "A empresa não possui um administrador ativo para receber a sessão.",
                EntrarComoResult.Motivos.SemAdminAtivo);
        }

        var principal = await principalDoUsuario.CriarAsync(admin);
        var identity = (ClaimsIdentity)principal.Identity!;
        identity.AddClaim(new Claim(TenantContext.ClaimImpersonando, "true"));
        identity.AddClaim(new Claim(TenantContext.ClaimSuperAdminOriginalId, superAdminId.ToString()));

        // Marca de início do teto ABSOLUTO (ver ValidadeAbsolutaImpersonacao): o cookie sliding e o
        // SecurityStampValidator renovam o ticket indefinidamente enquanto a sessão é usada, então o
        // ExpiresUtc abaixo sozinho só limita a sessão ociosa. Esta claim é copiada — com o valor
        // original — em toda reemissão do principal, e é a medida de duração que o cliente não
        // consegue esticar.
        var iniciadaEm = timeProvider.GetUtcNow();
        identity.AddClaim(new Claim(
            TenantContext.ClaimImpersonacaoIniciadaEm,
            iniciadaEm.ToString("O", CultureInfo.InvariantCulture)));

        // Quem assina a auditoria é o super admin que clicou — o principal LIDO ACIMA, antes de o
        // cookie ser trocado pelo do admin impersonado (o AuthenticationStateProvider do circuito
        // ainda teria o principal antigo; o HttpContext.User é a fonte confiável aqui).
        var superAdminNome = principalAtual.Identity?.Name;

        // SignInManager.SignInAsync só aceita um ApplicationUser; aqui o principal já foi montado
        // pela factory (com as claims de tenancy + impersonation), então emitimos o cookie
        // diretamente no esquema do Identity.
        // A expiração absoluta é curta e própria da impersonation (ver ValidadeImpersonacao): o
        // ticket não é um login comum do admin da empresa, e /central/voltar o troca por um
        // principal de super admin — não pode ficar aberto indefinidamente.
        var httpContext = httpContextAccessor.HttpContext!;
        await httpContext.SignInAsync(
            IdentityConstants.ApplicationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = iniciadaEm + ValidadeImpersonacao,
            });

        // Auditoria DEPOIS do sign-in bem-sucedido (decisão 4): se a troca de cookie falhasse, a
        // tentativa não teria acontecido. O save é só da auditoria — esta operação não altera
        // dados de domínio. Falha de auditoria aqui não pode desfazer o cookie já emitido, então
        // propaga (fail loud): o operador vê o erro em vez de a ação ficar sem trilha.
        db.RegistrosAuditoria.Add(new RegistroAuditoria(
            empresa.Id,
            superAdminId,
            superAdminNome,
            RegistroAuditoria.Acoes.EntrarComo,
            $"Super admin entrou como o administrador {admin.Email} da empresa '{empresa.Nome}'.",
            RegistroAuditoria.Origens.Central));
        await db.SaveChangesAsync(cancellationToken);

        return EntrarComoResult.Success();
    }

    /// <summary>
    /// Admin ativo da empresa, escolhido de forma determinística (menor <c>Id</c>) quando há mais
    /// de um. Filtra por empresa/ativo antes de checar a role para evitar carregar a base inteira.
    /// </summary>
    private async Task<ApplicationUser?> ObterAdminDaEmpresaAsync(Guid empresaId)
    {
        var admins = await userManager.GetUsersInRoleAsync(ApplicationRole.Admin);

        return admins
            .Where(u => u.EmpresaId == empresaId && u.Ativo)
            .OrderBy(u => u.Id)
            .FirstOrDefault();
    }

    private static Guid ObterIdUsuario(ClaimsPrincipal principal)
    {
        var valor = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(valor, out var id)
            ? id
            : throw new InvalidOperationException("O usuário autenticado não possui um identificador válido.");
    }

    private static bool LerBooleano(ClaimsPrincipal principal, string tipoClaim) =>
        bool.TryParse(principal.FindFirst(tipoClaim)?.Value, out var valor) && valor;
}