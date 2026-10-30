namespace AndreGas.Web.Features.Central.EntrarComo;

/// <summary>Resultado da tentativa de "Entrar como" uma empresa.</summary>
/// <param name="Succeeded">Indica se a sessão foi trocada para o admin da empresa.</param>
/// <param name="ErrorMessage">Mensagem pt-BR quando a entrada é negada (empresa inativa, sem admin).</param>
/// <param name="Destino">
/// Rota para onde o endpoint redireciona após a troca de cookie. A navegação no cliente deve ser
/// feita com <c>forceLoad: true</c> — o circuito Blazor em andamento manteria o principal e o
/// <c>AppDbContext</c> da empresa anterior (requisito duro da Task 2).
/// </param>
/// <param name="Motivo">
/// Código curto e estável do motivo da recusa (<see cref="Motivos"/>), usado pelo endpoint para
/// voltar à central com <c>?erro=...</c> e exibir a mensagem na tela — a alternativa (403) deixava
/// o super admin numa página de erro sem explicação. Nulo quando <paramref name="Succeeded"/>.
/// </param>
public sealed record EntrarComoResult(bool Succeeded, string? ErrorMessage, string Destino, string? Motivo = null)
{
    /// <summary>Códigos de motivo aceitos no <c>?erro=</c> da central.</summary>
    public static class Motivos
    {
        public const string EmpresaNaoEncontrada = "empresa-nao-encontrada";
        public const string EmpresaInativa = "empresa-inativa";
        public const string SemAdminAtivo = "sem-admin";
    }

    public static EntrarComoResult Success() => new(true, null, "/");

    public static EntrarComoResult Failure(string errorMessage, string motivo) =>
        new(false, errorMessage, "/", motivo);
}