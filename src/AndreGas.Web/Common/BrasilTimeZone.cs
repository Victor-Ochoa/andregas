namespace AndreGas.Web.Common;

/// <summary>
/// Fuso do negócio: horário de Brasília (America/Sao_Paulo, UTC−3, sem horário de verão desde 2019).
/// Usado pelos handlers de leitura para converter os <see cref="DateTime"/> UTC gravados no banco
/// (colunas timestamptz) para o horário local exibido nas telas e usado nos agrupamentos.
/// Utiliza o <c>tzdata</c> quando disponível (mais correto historicamente) e cai para um fuso
/// fixo UTC−3 se o dados de zona do SO não estiverem presentes (ex.: algum container mínimo).
/// </summary>
public static class BrasilTimeZone
{
    private const string IanaId = "America/Sao_Paulo";

    private static readonly Lazy<TimeZoneInfo> InstanciaInterna = new(static () =>
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(IanaId);
        }
        catch (TimeZoneNotFoundException)
        {
            // Host sem tzdata: cria um fuso de offset fixo UTC−3 (equivalente ao fuso atual de Brasília).
            return TimeZoneInfo.CreateCustomTimeZone(
                IanaId,
                TimeSpan.FromHours(-3),
                IanaId,
                "Horário Padrão de Brasília");
        }
    });

    /// <summary>Fuso único do app (singleton).</summary>
    public static TimeZoneInfo Instancia => InstanciaInterna.Value;

    /// <summary>
    /// Converte um instante UTC para o horário de Brasília. Se o <paramref name="utc"/> vier com
    /// <see cref="DateTimeKind.Unspecified"/>/<see cref="DateTimeKind.Local"/> (ex.: saída do Npgsql
    /// que não normalizou o Kind), trata como UTC — o banco grava UTC em colunas timestamptz.
    /// </summary>
    public static DateTime ParaBrasilia(DateTime utc)
    {
        if (utc.Kind != DateTimeKind.Utc)
        {
            utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTimeFromUtc(utc, Instancia);
    }

    /// <summary>
    /// Converte um horário de Brasília para UTC (para comparar com as colunas timestamptz).
    /// O <paramref name="local"/> deve ter Kind Unspecified/Local (meia-noite local etc.).
    /// </summary>
    public static DateTime ParaUtc(DateTime local)
    {
        var unspecified = local.Kind == DateTimeKind.Utc
            ? DateTime.SpecifyKind(local, DateTimeKind.Unspecified)
            : local;
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, Instancia);
    }
}