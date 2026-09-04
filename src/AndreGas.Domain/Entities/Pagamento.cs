using AndreGas.Domain.Enums;

namespace AndreGas.Domain.Entities;

/// <summary>
/// Registro de um pagamento recebido de um cliente. Pode representar o pagamento de uma venda
/// paga no ato (Pix, Débito, Crédito, Dinheiro, Gás do Povo) ou o abatimento de saldo devedor
/// (fiado). Vendas fiado não geram pagamento no momento da venda.
/// </summary>
public class Pagamento
{
    public Guid Id { get; private set; }
    public Guid ClienteId { get; private set; }
    public Cliente? Cliente { get; private set; }
    public decimal Valor { get; private set; }
    public DateTime Data { get; private set; }
    public string? Observacao { get; private set; }

    /// <summary>Forma de pagamento usada (Fiado nunca é usado em um pagamento).</summary>
    public FormaPagamento FormaPagamento { get; private set; }

    private Pagamento()
    {
    }

    public Pagamento(Guid clienteId, decimal valor, FormaPagamento formaPagamento, string? observacao = null, DateTime? data = null)
    {
        if (valor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valor), "O valor do pagamento deve ser maior que zero.");
        }

        if (formaPagamento == FormaPagamento.Fiado)
        {
            throw new ArgumentOutOfRangeException(nameof(formaPagamento), "A forma de pagamento de um pagamento de saldo não pode ser Fiado.");
        }

        Id = Guid.NewGuid();
        ClienteId = clienteId;
        Valor = valor;
        FormaPagamento = formaPagamento;
        Observacao = observacao;
        Data = data ?? DateTime.UtcNow;
    }
}
