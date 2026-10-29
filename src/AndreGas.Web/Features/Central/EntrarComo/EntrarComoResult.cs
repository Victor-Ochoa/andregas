namespace AndreGas.Web.Features.Central.EntrarComo;

/// <summary>Resultado da tentativa de "Entrar como" uma empresa.</summary>
/// <param name="Succeeded">Indica se a sessão foi trocada para o admin da empresa.</param>
/// <param name="ErrorMessage">Mensagem pt-BR quando a entrada é negada (empresa inativa, sem admin).</param>
/// <param name="Destino">
/// Rota para onde o endpoint redireciona após a troca de cookie. A navegação no cliente deve ser
/// feita com <c>forceLoad: true</c> — o circuito Blazor em andamento manteria o principal e o
/// <c>AppDbContext</c> da empresa anterior (requisito duro da Task 2).
/// </param>
public sealed record EntrarComoResult(bool Succeeded, string? ErrorMessage, string Destino)
{
    public static EntrarComoResult Success() => new(true, null, "/");

    public static EntrarComoResult Failure(string errorMessage) => new(false, errorMessage, "/");
}