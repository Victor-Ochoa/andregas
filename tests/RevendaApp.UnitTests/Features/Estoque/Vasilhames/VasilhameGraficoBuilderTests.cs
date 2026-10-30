using MudBlazor;

using RevendaApp.Web.Features.Estoque.Vasilhames;
using RevendaApp.Web.Features.Home;

namespace RevendaApp.UnitTests.Features.Estoque.Vasilhames;

public class VasilhameGraficoBuilderTests
{
    [Fact]
    public void ConstruirSeries_DeveMapearEstoqueVasilhameEEmprestado()
    {
        var itens = new[]
        {
            new VasilhameDashboardItem(Guid.NewGuid(), "Botijão 13kg", Estoque: 10, Vasilhame: 5, Emprestado: 2),
            new VasilhameDashboardItem(Guid.NewGuid(), "Botijão 5kg", Estoque: 3, Vasilhame: 1, Emprestado: 4),
        };

        var (series, labels) = VasilhameGraficoBuilder.Construir(itens);

        Assert.Equal(3, series.Count);
        Assert.Equal("Estoque", series[0].Name);
        Assert.Equal("Botijão 13kg", labels[0]);
        Assert.Equal(10d, series[0].Data.Values[0]);
        Assert.Equal(3d, series[0].Data.Values[1]);
        Assert.Equal("Vasilhame", series[1].Name);
        Assert.Equal(5d, series[1].Data.Values[0]);
        Assert.Equal(1d, series[1].Data.Values[1]);
        Assert.Equal("Emprestado", series[2].Name);
        Assert.Equal(2d, series[2].Data.Values[0]);
        Assert.Equal(4d, series[2].Data.Values[1]);
    }

    [Fact]
    public void Construir_ComListaVazia_DeveRetornarSemSeries()
    {
        var (series, labels) = VasilhameGraficoBuilder.Construir([]);

        Assert.Empty(series);
        Assert.Empty(labels);
    }

    [Fact]
    public void Construir_DeveUsarSequenciaZero_QuandoNaoHaValores()
    {
        var itens = new[]
        {
            new VasilhameDashboardItem(Guid.NewGuid(), "Botijão 13kg", Estoque: 0, Vasilhame: 0, Emprestado: 3),
        };

        var (series, labels) = VasilhameGraficoBuilder.Construir(itens);

        Assert.Equal(0, (int)series[0].Data.Values[0]);
        Assert.Equal(0, (int)series[1].Data.Values[0]);
        Assert.Equal(3d, series[2].Data.Values[0]);
    }

    [Fact]
    public void Construir_ComProdutoSemControle_DeveTerVasilhameEEmprestadoZerados()
    {
        var itens = new[]
        {
            new VasilhameDashboardItem(Guid.NewGuid(), "Galão Água 20L", Estoque: 7, Vasilhame: 0, Emprestado: 0),
        };

        var (series, _) = VasilhameGraficoBuilder.Construir(itens);

        Assert.Equal(7d, series[0].Data.Values[0]);
        Assert.Equal(0d, series[1].Data.Values[0]);
        Assert.Equal(0d, series[2].Data.Values[0]);
    }
}