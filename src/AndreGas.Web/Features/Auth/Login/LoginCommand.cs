using Mediator;

namespace AndreGas.Web.Features.Auth.Login;

/// <summary>Comando para autenticar um usuário com e-mail e senha.</summary>
public sealed record LoginCommand(string Email, string Password, bool RememberMe) : ICommand<LoginResult>;