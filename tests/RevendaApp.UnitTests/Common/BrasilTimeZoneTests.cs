using RevendaApp.Web.Common;

namespace RevendaApp.UnitTests.Common;

public class BrasilTimeZoneTests
{
    // Horário padrão de Brasília: UTC-3 (sem DST desde 2019).
    private static readonly TimeSpan OffsetBrasilia = TimeSpan.FromHours(-3);

    [Fact]
    public void ParaBrasilia_DeveConverterUtcParaBrasilia()
    {
        var utc = new DateTime(2026, 9, 15, 14, 0, 0, DateTimeKind.Utc);

        var brasilia = BrasilTimeZone.ParaBrasilia(utc);

        Assert.Equal(new DateTime(2026, 9, 15, 11, 0, 0), brasilia);
        Assert.Equal(DateTimeKind.Unspecified, brasilia.Kind);
    }

    [Fact]
    public void ParaBrasilia_TrataUnspecifiedComoUtc()
    {
        // Saída típica do Npgsql (timestamptz) vem sem Kind → deve ser tratada como UTC.
        var unspecified = new DateTime(2026, 9, 15, 14, 0, 0, DateTimeKind.Unspecified);

        var brasilia = BrasilTimeZone.ParaBrasilia(unspecified);

        Assert.Equal(new DateTime(2026, 9, 15, 11, 0, 0), brasilia);
    }

    [Fact]
    public void ParaBrasilia_TrataLocalComoUtc()
    {
        var local = new DateTime(2026, 9, 15, 14, 0, 0, DateTimeKind.Local);

        var brasilia = BrasilTimeZone.ParaBrasilia(local);

        // O parâmetro é um instante UTC (apenas com Kind errado) — é convertido como UTC.
        Assert.Equal(new DateTime(2026, 9, 15, 11, 0, 0), brasilia);
    }

    [Fact]
    public void ParaUtc_DeveConverterBrasiliaParaUtc()
    {
        var brasilia = new DateTime(2026, 9, 15, 11, 0, 0, DateTimeKind.Unspecified);

        var utc = BrasilTimeZone.ParaUtc(brasilia);

        Assert.Equal(new DateTime(2026, 9, 15, 14, 0, 0, DateTimeKind.Utc), utc);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
    }

    [Fact]
    public void ParaUtc_TrataKindLocalComoBrasiliaLocal()
    {
        var local = new DateTime(2026, 9, 15, 11, 0, 0, DateTimeKind.Local);

        var utc = BrasilTimeZone.ParaUtc(local);

        Assert.Equal(new DateTime(2026, 9, 15, 14, 0, 0, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void ParaUtc_TrataKindUtcComoBrasilia_EvitaDuplaConversao()
    {
        // Se passarem um valor UTC com Kind.Utc (ex.: comparar meia-noite local), deve re-criar o
        // valor APENAS como momento de Brasília (string local), não converter o instante.
        var utc = new DateTime(2026, 9, 15, 11, 0, 0, DateTimeKind.Utc);

        var resultado = BrasilTimeZone.ParaUtc(utc);

        Assert.Equal(new DateTime(2026, 9, 15, 14, 0, 0, DateTimeKind.Utc), resultado);
    }

    [Fact]
    public void ParaBrasilia_ParaUtc_DeveSerRedondo()
    {
        var utc = new DateTime(2026, 9, 15, 23, 45, 0, DateTimeKind.Utc);

        var brasilia = BrasilTimeZone.ParaBrasilia(utc);
        var deVolta = BrasilTimeZone.ParaUtc(brasilia);

        Assert.Equal(utc.ToUniversalTime(), deVolta);
    }

    [Fact]
    public void ParaBrasilia_NaViradaDeDiaLocal()
    {
        // 23:30 UTC = 20:30 de Brasília (mesmo dia); 02:00 UTC = 23:00 de Brasília (véspera).
        Assert.Equal(new DateTime(2026, 9, 15, 20, 30, 0), BrasilTimeZone.ParaBrasilia(new DateTime(2026, 9, 15, 23, 30, 0, DateTimeKind.Utc)));
        Assert.Equal(new DateTime(2026, 9, 15, 23, 0, 0), BrasilTimeZone.ParaBrasilia(new DateTime(2026, 9, 16, 2, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(new DateTime(2026, 9, 14, 21, 0, 0), BrasilTimeZone.ParaBrasilia(new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Instancia_DeveTerOffsetDeBrasilia()
    {
        Assert.Equal(OffsetBrasilia, BrasilTimeZone.Instancia.GetUtcOffset(DateTime.UtcNow));
    }

    [Fact]
    public void ParaBrasilia_DeveManterPrecisaoDeSegundos()
    {
        var utc = new DateTime(2026, 9, 15, 14, 30, 59, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 9, 15, 11, 30, 59), BrasilTimeZone.ParaBrasilia(utc));
    }
}