namespace AndreGas.Domain.Enums;

/// <summary>
/// Janela de tempo usada para filtrar os valores do dashboard da home.
/// Períodos relativos (UltimaHora, Ultimas3Horas, Ultimas24Horas, Ultimos7Dias) são
/// calculados a partir do momento atual; Hoje e EsteMes seguem o calendário; Tudo ignora
/// o filtro de data.
/// </summary>
public enum PeriodoDashboard
{
    UltimaHora,
    Ultimas3Horas,
    Hoje,
    Ultimas24Horas,
    Ultimos7Dias,
    EsteMes,
    Tudo,
}