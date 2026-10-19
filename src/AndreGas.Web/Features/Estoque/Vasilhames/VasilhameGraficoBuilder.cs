using MudBlazor;

namespace AndreGas.Web.Features.Estoque.Vasilhames;

/// <summary>
/// Transforma o controle de vasilhames em séries/labels para o gráfico de barras empilhadas
/// da tela de estoque. Separado para permitir teste unitário puro (sem Razor/DB).
/// </summary>
public static class VasilhameGraficoBuilder
{
    /// <summary>Retorna as séries (Disponíveis, Emprestados) e os rótulos (nomes dos produtos).</summary>
    public static (IReadOnlyList<ChartSeries<double>> Series, string[] Labels) Construir(
        IReadOnlyList<ControleVasilhameItem> itens)
    {
        if (itens.Count == 0)
        {
            return ([], []);
        }

        var labels = itens.Select(i => i.Nome).ToArray();
        var series = new List<ChartSeries<double>>
        {
            new()
            {
                Name = "Disponíveis",
                Data = itens.Select(i => (double)i.Disponivel).ToArray(),
            },
            new()
            {
                Name = "Emprestados",
                Data = itens.Select(i => (double)i.Emprestados).ToArray(),
            },
        };

        return (series, labels);
    }
}