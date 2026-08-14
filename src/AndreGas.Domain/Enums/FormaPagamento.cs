namespace AndreGas.Domain.Enums;

/// <summary>
/// Formas de pagamento aceitas em uma venda.
/// </summary>
public enum FormaPagamento
{
    Pix,
    Debito,
    Credito,
    Dinheiro,

    /// <summary>
    /// Venda a prazo — soma o valor total ao saldo devedor do cliente.
    /// </summary>
    Fiado,

    /// <summary>
    /// Programa social/governo — pago integralmente no ato, mas usa o preço
    /// subsidiado (<see cref="Entities.Produto.PrecoGasDoPovo"/>) em vez do preço normal.
    /// </summary>
    GasDoPovo,
}
