using AndreGas.Web.Theme;

using MudBlazor;
using MudBlazor.Utilities;

namespace AndreGas.UnitTests.Theme;

/// <summary>
/// Testes do <see cref="AndreGasTheme.ComCorPrimaria"/>: tema derivado da cor primária da empresa,
/// com cache estático para não alocar um <c>MudTheme</c> novo a cada render.
/// </summary>
public class AndreGasThemeTests
{
    [Fact]
    public void ComCorPrimaria_aplica_a_cor_no_primary_e_no_appbar()
    {
        var tema = AndreGasTheme.ComCorPrimaria("#CC0000");

        Assert.Equal(new MudColor("#CC0000"), tema.PaletteLight.Primary);
        Assert.Equal(new MudColor("#CC0000"), tema.PaletteLight.AppbarBackground);

        // As variações são derivadas da cor: escurecida e clareada, nunca iguais à cor base.
        var escurecida = new MudColor(tema.PaletteLight.PrimaryDarken);
        var clareada = new MudColor(tema.PaletteLight.PrimaryLighten);

        Assert.NotEqual(new MudColor("#CC0000"), escurecida);
        Assert.NotEqual(new MudColor("#CC0000"), clareada);
        Assert.True(SomarCanais(escurecida) < SomarCanais(new MudColor("#CC0000")));
        Assert.True(SomarCanais(clareada) > SomarCanais(new MudColor("#CC0000")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("vermelho")]
    [InlineData("#12345")]
    [InlineData("0A1F44")]
    public void ComCorPrimaria_com_cor_invalida_ou_nula_devolve_tema_padrao(string? cor)
    {
        Assert.Same(AndreGasTheme.Default, AndreGasTheme.ComCorPrimaria(cor));
    }

    [Fact]
    public void ComCorPrimaria_devolve_a_mesma_instancia_para_a_mesma_cor()
    {
        var primeira = AndreGasTheme.ComCorPrimaria("#CC0000");
        var segunda = AndreGasTheme.ComCorPrimaria("#CC0000");

        Assert.Same(primeira, segunda);

        // A chave do cache é case-insensitive: a mesma cor em caixa baixa devolve a mesma instância.
        Assert.Same(primeira, AndreGasTheme.ComCorPrimaria("#cc0000"));

        // E #RGB é normalizado para #RRGGBB antes de virar chave: #C00 cai no mesmo tema de #CC0000.
        Assert.Same(primeira, AndreGasTheme.ComCorPrimaria("#C00"));
    }

    [Fact]
    public void ComCorPrimaria_com_a_cor_padrao_devolve_o_tema_padrao()
    {
        // O azul marinho padrão tem tons ajustados à mão; derivá-los mudaria o visual atual.
        Assert.Same(AndreGasTheme.Default, AndreGasTheme.ComCorPrimaria(AndreGasTheme.CorPrimariaPadrao));
    }

    [Fact]
    public void ComCorPrimaria_usa_texto_escuro_sobre_cor_clara()
    {
        // Sem isso, uma empresa com cor clara teria texto branco ilegível no app bar.
        var tema = AndreGasTheme.ComCorPrimaria("#FFEB3B");

        Assert.Equal(new MudColor("#000000"), tema.PaletteLight.AppbarText);
        Assert.Equal(new MudColor("#000000"), tema.PaletteLight.PrimaryContrastText);
    }

    [Fact]
    public void ComCorPrimaria_usa_texto_branco_sobre_cor_escura()
    {
        var tema = AndreGasTheme.ComCorPrimaria("#0B1B2B");

        Assert.Equal(new MudColor("#FFFFFF"), tema.PaletteLight.AppbarText);
    }

    [Fact]
    public void ComCorPrimaria_nao_compartilha_typography_ou_layout_com_o_tema_padrao()
    {
        // São classes mutáveis: compartilhar a instância faria um ajuste em um tema de empresa
        // vazar para todas as outras (e o cache não detectaria).
        var tema = AndreGasTheme.ComCorPrimaria("#CC0000");

        Assert.NotSame(AndreGasTheme.Default.Typography, tema.Typography);
        Assert.NotSame(AndreGasTheme.Default.LayoutProperties, tema.LayoutProperties);

        // Mesmos valores do padrão (o visual não muda), mas objetos independentes.
        Assert.Equal(
            AndreGasTheme.Default.Typography.Default.FontFamily,
            tema.Typography.Default.FontFamily);
        Assert.Equal(
            AndreGasTheme.Default.LayoutProperties.DefaultBorderRadius,
            tema.LayoutProperties.DefaultBorderRadius);
    }

    private static int SomarCanais(MudColor cor) => cor.R + cor.G + cor.B;
}