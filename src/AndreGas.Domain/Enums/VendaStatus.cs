namespace AndreGas.Domain.Enums;

/// <summary>
/// Situação de pagamento de uma venda.
/// </summary>
public enum VendaStatus
{
    /// <summary>Venda paga integralmente no ato (qualquer forma de pagamento exceto Fiado).</summary>
    Pago,

    /// <summary>Venda fiado com saldo ainda em aberto.</summary>
    FiadoAberto,

    /// <summary>Venda fiado já quitada por pagamento(s) do cliente.</summary>
    FiadoQuitado,
}
