using AndreGas.Domain.Entities;
using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Identity;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Auth;

/// <summary>Motivo pelo qual um usuário existente não pode receber (ou renovar) uma sessão.</summary>
public enum MotivoRecusaAcesso
{
    /// <summary><see cref="ApplicationUser.Ativo"/> = <c>false</c>.</summary>
    UsuarioInativo,

    /// <summary>A empresa do usuário está desativada (ou não existe mais).</summary>
    EmpresaInativa,
}

/// <summary>
/// <b>Regra única</b> de acesso de multi-empresa: um usuário só pode autenticar (ou ter a sessão
/// renovada) se estiver <b>ativo</b> e, quando pertence a uma empresa, se a empresa estiver
/// <b>ativa</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por que centralizar:</b> a mesma regra vale em dois pontos — o login
/// (<see cref="Login.LoginCommandHandler"/>, antes de emitir o cookie) e a recarga de sessão
/// (<c>POST /configuracoes/atualizar-sessao</c>, que reemite o cookie do usuário autenticado). Duas
/// cópias divergiram uma vez: a recarga passou a reemitir o ticket <b>sem</b> revalidar o estado do
/// usuário/empresa, o que anulava a revogação de sessão (um usuário inativado renovava o próprio
/// acesso indefinidamente). Uma regra, um lugar.
/// </para>
/// <para>
/// <b>Usuário inexistente não é tratado aqui:</b> o login não pode revelar a existência do e-mail
/// (deixa o sign-in responder genericamente); a recarga, que já tem um principal autenticado, trata
/// o usuário nulo como sessão inválida antes de chamar esta regra.
/// </para>
/// </remarks>
public static class RegraDeAcesso
{
    /// <summary>
    /// Avalia a regra de acesso. Devolve <c>null</c> quando o usuário pode autenticar, ou o motivo
    /// da recusa.
    /// </summary>
    public static async Task<MotivoRecusaAcesso?> PodeAutenticarAsync(
        ApplicationUser usuario,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        if (!usuario.Ativo)
        {
            return MotivoRecusaAcesso.UsuarioInativo;
        }

        if (usuario.EmpresaId is { } empresaId)
        {
            // Empresa inexistente é tratada como inativa (fail-closed): sem a linha da empresa não
            // há como operar o tenant.
            var empresaAtiva = await db.Set<Empresa>()
                .AsNoTracking()
                .AnyAsync(e => e.Id == empresaId && e.Ativo, cancellationToken);

            if (!empresaAtiva)
            {
                return MotivoRecusaAcesso.EmpresaInativa;
            }
        }

        return null;
    }

    /// <summary>Mensagem ao usuário para cada motivo de recusa (mesma redação do login).</summary>
    public static string Mensagem(MotivoRecusaAcesso motivo) => motivo switch
    {
        MotivoRecusaAcesso.UsuarioInativo => "Usuário desativado. Fale com o administrador.",
        MotivoRecusaAcesso.EmpresaInativa => "Empresa desativada. Fale com o administrador.",
        _ => throw new ArgumentOutOfRangeException(nameof(motivo), motivo, "Motivo de recusa desconhecido."),
    };
}