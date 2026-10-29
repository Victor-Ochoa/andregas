using System.Collections.Concurrent;
using System.Globalization;

using MudBlazor;

namespace AndreGas.Web.Theme;

/// <summary>
/// Tema visual do produto "Revenda.App" (cor primária configurável por empresa): azul marinho
/// padrão, com o app bar e o primary derivados da cor da empresa quando ela define uma.
/// </summary>
public static class AndreGasTheme
{
    /// <summary>Cor primária padrão (azul marinho), usada quando a empresa não define uma cor.</summary>
    public const string CorPrimariaPadrao = "#0A1F44";

    private const string NavyBlue = CorPrimariaPadrao;
    private const string NavyBlueLight = "#16305F";
    private const string NavyBlueDark = "#050F22";
    private const string White = "#FFFFFF";
    private const string SoftBlue = "#E8F1FF";
    private const string Accent = "#1D4ED8";
    private const string Success = "#16A34A";
    private const string Warning = "#F59E0B";
    private const string Danger = "#DC2626";

    /// <summary>
    /// Temas já derivados, por cor normalizada (caixa alta). Evita alocar um <see cref="MudTheme"/>
    /// novo a cada render do app bar — o layout é renderizado em toda navegação.
    /// </summary>
    private static readonly ConcurrentDictionary<string, MudTheme> TemasPorCor =
        new(StringComparer.OrdinalIgnoreCase);

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

    /// <summary>
    /// Devolve o tema com <c>Primary</c>/<c>AppbarBackground</c> na cor da empresa e as variações
    /// <c>PrimaryDarken</c>/<c>PrimaryLighten</c> derivadas dela. Cor nula, fora do formato
    /// <c>#RRGGBB</c>/<c>#RGB</c> ou igual à cor padrão devolve o <see cref="Default"/> (os tons do
    /// azul marinho são ajustados à mão, não derivados).
    /// </summary>
    /// <param name="corPrimaria">Cor primária em hex (ex.: <c>#CC0000</c>).</param>
    public static MudTheme ComCorPrimaria(string? corPrimaria)
    {
        var chave = NormalizarCor(corPrimaria);

        // Cor padrão (ou ausente/inválida) → tema padrão: a chave vazia aponta para Default.
        if (chave is null || string.Equals(chave, CorPrimariaPadrao, StringComparison.OrdinalIgnoreCase))
        {
            chave = string.Empty;
        }

        return TemasPorCor.GetOrAdd(chave, static chave =>
            chave.Length == 0 ? Default : CriarTema(chave));
    }

    /// <summary>Normaliza <c>#RGB</c> para <c>#RRGGBB</c> em caixa alta; inválida → <c>null</c>.</summary>
    private static string? NormalizarCor(string? cor)
    {
        if (string.IsNullOrWhiteSpace(cor))
        {
            return null;
        }

        var valor = cor.Trim();
        if (valor.Length > 0 && valor[0] != '#')
        {
            return null;
        }

        var digitos = valor[1..];
        if (digitos.Length == 3 && digitos.All(Uri.IsHexDigit))
        {
            digitos = string.Concat(digitos.Select(d => new string(d, 2)));
        }

        if (digitos.Length != 6 || !digitos.All(Uri.IsHexDigit))
        {
            return null;
        }

        return "#" + digitos.ToUpperInvariant();
    }

    private static MudTheme CriarTema(string cor)
    {
        var baseTheme = Default;

        // Texto sobre a cor da empresa: o MudBlazor NÃO deriva contraste automaticamente (mantém
        // branco), então uma empresa com cor clara (ex.: #FFEB3B) ficaria com texto branco ilegível
        // no app bar e nos botões. Escolhe preto ou branco pela luminância da cor.
        var contraste = CorDeContraste(cor);

        return new MudTheme
        {
            PaletteLight = new PaletteLight
            {
                Primary = cor,
                PrimaryDarken = Escurecer(cor, 0.20),
                PrimaryLighten = Clarear(cor, 0.15),
                PrimaryContrastText = contraste,
                Secondary = baseTheme.PaletteLight.Secondary,
                Tertiary = baseTheme.PaletteLight.Tertiary,
                Info = baseTheme.PaletteLight.Info,
                Success = baseTheme.PaletteLight.Success,
                Warning = baseTheme.PaletteLight.Warning,
                Error = baseTheme.PaletteLight.Error,
                AppbarBackground = cor,
                AppbarText = contraste,
                Background = baseTheme.PaletteLight.Background,
                Surface = baseTheme.PaletteLight.Surface,
                DrawerBackground = baseTheme.PaletteLight.DrawerBackground,
                DrawerText = baseTheme.PaletteLight.DrawerText,
                TextPrimary = baseTheme.PaletteLight.TextPrimary,
                TextSecondary = baseTheme.PaletteLight.TextSecondary,
            },
            PaletteDark = new PaletteDark
            {
                Primary = Clarear(cor, 0.10),
                Secondary = baseTheme.PaletteDark.Secondary,
                AppbarBackground = Escurecer(cor, 0.35),
                Background = baseTheme.PaletteDark.Background,
                Surface = baseTheme.PaletteDark.Surface,
            },
            // Typography/LayoutProperties são CLASSES mutáveis: reaproveitar as instâncias do
            // Default faria qualquer consumidor que ajustasse a fonte/raio de um tema de empresa
            // contaminar o tema de todas as outras (e o cache não detectaria, é a mesma instância).
            Typography = new Typography
            {
                Default = new DefaultTypography
                {
                    FontFamily = [.. baseTheme.Typography.Default?.FontFamily ?? []],
                },
            },
            LayoutProperties = new LayoutProperties
            {
                DefaultBorderRadius = baseTheme.LayoutProperties.DefaultBorderRadius,
                DrawerWidthLeft = baseTheme.LayoutProperties.DrawerWidthLeft,
            },
        };
    }

    /// <summary>
    /// Cor de texto legível sobre <paramref name="cor"/>: preto para cores claras, branco para
    /// escuras. Usa a luminância relativa (WCAG), com limiar 0.5.
    /// </summary>
    private static string CorDeContraste(string cor)
    {
        var r = LerCanal(cor, 1) / 255d;
        var g = LerCanal(cor, 3) / 255d;
        var b = LerCanal(cor, 5) / 255d;

        static double Linearizar(double canal) =>
            canal <= 0.03928 ? canal / 12.92 : Math.Pow((canal + 0.055) / 1.055, 2.4);

        var luminancia = (0.2126 * Linearizar(r)) + (0.7152 * Linearizar(g)) + (0.0722 * Linearizar(b));

        return luminancia > 0.5 ? "#000000" : White;
    }

    /// <summary>Escurece a cor reduzindo cada canal RGB na proporção informada (0..1).</summary>
    private static string Escurecer(string cor, double proporcao) =>
        Ajustar(cor, canal => (int)Math.Round(canal * (1 - proporcao)));

    /// <summary>Clareia a cor aproximando cada canal RGB do branco na proporção informada (0..1).</summary>
    private static string Clarear(string cor, double proporcao) =>
        Ajustar(cor, canal => (int)Math.Round(canal + ((255 - canal) * proporcao)));

    private static string Ajustar(string cor, Func<int, int> ajuste)
    {
        var canais = new[]
        {
            LerCanal(cor, 1),
            LerCanal(cor, 3),
            LerCanal(cor, 5),
        };

        return "#" + string.Concat(canais.Select(canal =>
            Math.Clamp(ajuste(canal), 0, 255).ToString("X2", CultureInfo.InvariantCulture)));
    }

    private static int LerCanal(string cor, int inicio) =>
        int.Parse(cor.AsSpan(inicio, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}