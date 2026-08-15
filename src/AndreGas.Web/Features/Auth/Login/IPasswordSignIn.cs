namespace AndreGas.Web.Features.Auth.Login;

/// <summary>Resultado possível de uma tentativa de login por senha.</summary>
public enum SignInOutcome
{
    Succeeded,
    Failed,
    LockedOut,
    NotAllowed,
}

/// <summary>
/// Abstração fina sobre o <c>SignInManager</c> do ASP.NET Core Identity, usada apenas para
/// permitir testar o <see cref="LoginCommandHandler"/> sem depender diretamente de uma classe
/// difícil de simular (SignInManager tem um construtor com muitas dependências).
/// </summary>
public interface IPasswordSignIn
{
    Task<SignInOutcome> PasswordSignInAsync(string email, string password, bool rememberMe);
}
