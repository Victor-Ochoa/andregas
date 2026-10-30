namespace RevendaApp.Web.Features.Auth.Login;

/// <summary>Resultado da tentativa de login.</summary>
/// <param name="Succeeded">Indica se a autenticação foi bem-sucedida.</param>
/// <param name="ErrorMessage">Mensagem para o usuário quando a autenticação falha.</param>
/// <param name="Destino">
/// Rota para onde o login deve navegar após o sucesso: <c>/central</c> para o super admin da
/// central (sem empresa) e <c>/</c> para os usuários de empresa. Decidido no
/// <see cref="LoginCommandHandler"/> porque a tela de login é SSR estática (não consulta o
/// usuário autenticado para decidir a navegação).
/// </param>
public sealed record LoginResult(bool Succeeded, string? ErrorMessage, string Destino = "/")
{
    public static LoginResult Success(string destino = "/") => new(true, null, destino);

    public static LoginResult Failure(string errorMessage) => new(false, errorMessage, "/");
}