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
    private const string SoftBlue = "#E8F1FF";
    private const string Accent = "#1D4ED8";
    private const string Success = "#16A34A";
    private const string Warning = "#F59E0B";
    private const string Danger = "#DC2626";

    public static MudTheme Default { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = NavyBlue,
            PrimaryDarken = NavyBlueDark,
            PrimaryLighten = NavyBlueLight,
            Secondary = Accent,
            Tertiary = SoftBlue,
            Info = Accent,
            Success = Success,
            Warning = Warning,
            Error = Danger,
            AppbarBackground = NavyBlue,
            AppbarText = White,
            Background = "#F4F7FB",
            Surface = White,
            DrawerBackground = White,
            DrawerText = NavyBlue,
            TextPrimary = NavyBlueDark,
            TextSecondary = "#4B5F7A",
        },
        PaletteDark = new PaletteDark
        {
            Primary = NavyBlueLight,
            Secondary = Accent,
            AppbarBackground = NavyBlueDark,
            Background = "#0B1220",
            Surface = "#121C2B",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Segoe UI", "Helvetica Neue", "Arial", "sans-serif"],
            },
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "14px",
            DrawerWidthLeft = "260px",
        },
    };
}
