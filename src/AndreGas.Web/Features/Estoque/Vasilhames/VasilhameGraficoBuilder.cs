using AndreGas.Web.Features.Home;

using MudBlazor;

namespace AndreGas.Web.Features.Estoque.Vasilhames;

/// <summary>
/// Transforma o estado físico de <b>todos</b> os produtos em séries/labels para o gráfico de barras
/// da Home. <see cref="VasilhameDashboardItem"/> expõe Estoque/Vasilhame/Emprestado de cada produto
/// (produtos sem controle ficam com Vasilhame e Emprestado zerados).
/// Separado para permitir teste unitário puro (sem Razor/DB).
/// </summary>
public static class VasilhameGraficoBuilder
{
    /// <summary>Retorna as séries (Estoque, Vasilhame, Emprestado) e os rótulos (nomes dos produtos).</summary>
    public static (IReadOnlyList<ChartSeries<double>> Series, string[] Labels) Construir(
        IReadOnlyList<VasilhameDashboardItem> itens)
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
                Name = "Estoque",
                Data = itens.Select(i => (double)i.Estoque).ToArray(),
            },
            new()
            {
                Name = "Vasilhame",
                Data = itens.Select(i => (double)i.Vasilhame).ToArray(),
            },
            new()
            {
                Name = "Emprestado",
                Data = itens.Select(i => (double)i.Emprestado).ToArray(),
            },
        };

        return (series, labels);
    }
}