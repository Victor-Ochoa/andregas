namespace AndreGas.Web.Features.Central.VoltarCentral;

/// <summary>Resultado da tentativa de "Voltar à central".</summary>
/// <param name="Succeeded">Indica se a sessão voltou a ser a do super admin original.</param>
/// <param name="ErrorMessage">Mensagem pt-BR quando a volta é negada.</param>
/// <param name="Destino">
/// Rota para onde o endpoint redireciona após reemitir o cookie (<c>/central</c>). A navegação no
/// cliente deve ser feita com <c>forceLoad: true</c> — o circuito manteria o principal impersonado.
/// </param>
public sealed record VoltarCentralResult(bool Succeeded, string? ErrorMessage, string Destino)
{
    /// <summary>Rota da central do super admin.</summary>
    public const string DestinoCentral = "/central";

    public static VoltarCentralResult Success() => new(true, null, DestinoCentral);

    public static VoltarCentralResult Failure(string errorMessage) => new(false, errorMessage, DestinoCentral);
}