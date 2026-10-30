using Mediator;

using Microsoft.AspNetCore.Identity;

using RevendaApp.Domain.Entities;
using RevendaApp.Infrastructure;
using RevendaApp.Infrastructure.Identity;

namespace RevendaApp.Web.Features.Auth.Login;

/// <summary>
/// Autentica o usuário com e-mail e senha. Antes de emitir o cookie, valida o estado de
/// multi-empresa: <see cref="ApplicationUser.Ativo"/> e, quando o usuário pertence a uma empresa,
/// <see cref="Empresa.Ativo"/>. Decide também o destino pós-login (<c>/central</c> para o super
/// admin da central, <c>/</c> para usuários de empresa) — a tela de login é SSR estática e apenas
/// navega para onde o resultado manda.
/// </summary>
public sealed class LoginCommandHandler(
    IPasswordSignIn passwordSignIn,
    UserManager<ApplicationUser> userManager,
    AppDbContext db)
    : ICommandHandler<LoginCommand, LoginResult>
{
    /// <summary>Rota da central do super admin (usuário sem empresa).</summary>
    public const string DestinoCentral = "/central";

    /// <summary>Rota padrão do app da empresa.</summary>
    public const string DestinoApp = "/";

    public async ValueTask<LoginResult> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        // Carrega o usuário ANTES do sign-in: um usuário/empresa desativado não pode sequer
        // receber cookie. FindByEmailAsync normaliza o e-mail (mesma regra do Identity), evitando
        // que uma variação de caixa burle as verificações de "ativo".
        var usuario = await userManager.FindByEmailAsync(command.Email);

        if (usuario is not null)
        {
            // Regra de acesso compartilhada com a recarga de sessão
            // (POST /configuracoes/atualizar-sessao): usuário ativo + empresa ativa.
            if (await RegraDeAcesso.PodeAutenticarAsync(usuario, db, cancellationToken) is { } motivo)
            {
                return LoginResult.Failure(RegraDeAcesso.Mensagem(motivo));
            }
        }

        var outcome = await passwordSignIn.PasswordSignInAsync(command.Email, command.Password, command.RememberMe);

        return outcome switch
        {
            SignInOutcome.Succeeded => LoginResult.Success(await ObterDestinoAsync(usuario)),
            SignInOutcome.LockedOut => LoginResult.Failure("Conta bloqueada temporariamente por excesso de tentativas. Tente novamente mais tarde."),
            SignInOutcome.NotAllowed => LoginResult.Failure("Este login não é permitido para esta conta."),
            _ => LoginResult.Failure("E-mail ou senha inválidos."),
        };
    }

    /// <summary>
    /// Super admin da central (sem empresa) cai em <c>/central</c>; qualquer usuário de empresa cai
    /// no app (<c>/</c>).
    /// </summary>
    private async Task<string> ObterDestinoAsync(ApplicationUser? usuario)
    {
        if (usuario is { EmpresaId: null } && await userManager.IsInRoleAsync(usuario, ApplicationRole.SuperAdmin))
        {
            return DestinoCentral;
        }

        return DestinoApp;
    }
}