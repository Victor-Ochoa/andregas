using Mediator;

namespace AndreGas.Web.Features.Auth.Login;

public sealed class LoginCommandHandler(IPasswordSignIn passwordSignIn) : ICommandHandler<LoginCommand, LoginResult>
{
    public async ValueTask<LoginResult> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var outcome = await passwordSignIn.PasswordSignInAsync(command.Email, command.Password, command.RememberMe);

        return outcome switch
        {
            SignInOutcome.Succeeded => LoginResult.Success(),
            SignInOutcome.LockedOut => LoginResult.Failure("Conta bloqueada temporariamente por excesso de tentativas. Tente novamente mais tarde."),
            SignInOutcome.NotAllowed => LoginResult.Failure("Este login não é permitido para esta conta."),
            _ => LoginResult.Failure("E-mail ou senha inválidos."),
        };
    }
}