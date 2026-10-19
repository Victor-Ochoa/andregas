using AndreGas.Web.Features.Estoque.Vasilhames;

using MudBlazor;

namespace AndreGas.UnitTests.Features.Estoque.Vasilhames;

public class VasilhameGraficoBuilderTests
{
    [Fact]
    public void ConstruirSeries_DeveMapearDisponiveisEEmprestados()
    {
        var itens = new[]
        {
            new ControleVasilhameItem(Guid.NewGuid(), "Botijão 13kg", Disponivel: 5, Emprestados: 2, Total: 7, []),
            new ControleVasilhameItem(Guid.NewGuid(), "Botijão 5kg", Disponivel: 1, Emprestados: 4, Total: 5, []),
        };

        var (series, labels) = VasilhameGraficoBuilder.Construir(itens);

        Assert.Equal(2, series.Count);
        Assert.Equal("Disponíveis", series[0].Name);
        Assert.Equal("Botijão 13kg", labels[0]);
        Assert.Equal(5d, series[0].Data.Values[0]);
        Assert.Equal(1d, series[0].Data.Values[1]);
        Assert.Equal("Emprestados", series[1].Name);
        Assert.Equal(2d, series[1].Data.Values[0]);
        Assert.Equal(4d, series[1].Data.Values[1]);
    }

    [Fact]
    public void Construir_ComListaVazia_DeveRetornarSemSeries()
    {
        var (series, labels) = VasilhameGraficoBuilder.Construir([]);

        Assert.Empty(series);
        Assert.Empty(labels);
    }

    [Fact]
    public void Construir_DeveUsarSequenciaZero_QuandoNaoHaDisponiveis()
    {
        var itens = new[]
        {
            new ControleVasilhameItem(Guid.NewGuid(), "Botijão 13kg", Disponivel: 0, Emprestados: 3, Total: 3, []),
        };

        var (series, labels) = VasilhameGraficoBuilder.Construir(itens);

        Assert.Equal(0, (int)series[0].Data.Values[0]);
        Assert.Equal(3d, series[1].Data.Values[0]);
    }
}