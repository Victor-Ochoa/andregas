using MudBlazor;

namespace AndreGas.Web.Theme;

/// <summary>
/// Tema visual da marca "André Gas e Água": azul marinho e branco.
/// </summary>
public static class AndreGasTheme
{
    private const string NavyBlue = "#0A1F44";
    private const string NavyBlueLight = "#16305F";
    private const string NavyBlueDark = "#050F22";
    private const string White = "#FFFFFF";

    public static MudTheme Default { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = NavyBlue,
            PrimaryDarken = NavyBlueDark,
            PrimaryLighten = NavyBlueLight,
            Secondary = White,
            AppbarBackground = NavyBlue,
            AppbarText = White,
            Background = White,
            Surface = White,
            DrawerBackground = White,
            DrawerText = NavyBlue,
        },
        PaletteDark = new PaletteDark
        {
            Primary = NavyBlueLight,
            Secondary = White,
            AppbarBackground = NavyBlueDark,
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Segoe UI", "Helvetica Neue", "Arial", "sans-serif"],
            },
        },
    };
}
