namespace AndreGas.Web.Features.Auth.Login;

/// <summary>Resultado da tentativa de login.</summary>
public sealed record LoginResult(bool Succeeded, string? ErrorMessage)
{
    public static LoginResult Success() => new(true, null);

    public static LoginResult Failure(string errorMessage) => new(false, errorMessage);
}