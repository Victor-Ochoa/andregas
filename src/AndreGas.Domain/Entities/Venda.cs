using AndreGas.Domain.Enums;

namespace AndreGas.Domain.Entities;

/// <summary>
/// Registro de uma venda: itens vendidos, forma de pagamento e desconto aplicado.
/// O desconto é único por venda e é rateado proporcionalmente entre os itens para
/// calcular o lucro líquido de cada um.
/// </summary>
public class Venda
{
    private readonly List<ItemVenda> _itens = [];

    public Guid Id { get; private set; }
    public Guid ClienteId { get; private set; }
    public Cliente? Cliente { get; private set; }
    public DateTime DataHora { get; private set; }
    public FormaPagamento FormaPagamento { get; private set; }
    public decimal Desconto { get; private set; }

    /// <summary>Situação de pagamento da venda, persistida no banco.</summary>
    public VendaStatus Status { get; private set; }

    public IReadOnlyCollection<ItemVenda> Itens => _itens.AsReadOnly();

    /// <summary>Soma dos itens antes do desconto.</summary>
    public decimal ValorBruto => _itens.Sum(i => i.ValorTotal);

    /// <summary>Valor final da venda, já líquido do desconto — é o que soma ao saldo devedor quando Fiado.</summary>
    public decimal ValorTotal => ValorBruto - Desconto;

    /// <summary>Soma do lucro líquido (já descontado) de todos os itens.</summary>
    public decimal LucroTotal => _itens.Sum(i => i.Lucro);

    private Venda()
    {
    }

    public Venda(Guid clienteId, FormaPagamento formaPagamento, DateTime? dataHora = null)
    {
        Id = Guid.NewGuid();
        ClienteId = clienteId;
        FormaPagamento = formaPagamento;
        DataHora = dataHora ?? DateTime.UtcNow;
        Desconto = 0m;
        Status = formaPagamento == FormaPagamento.Fiado ? VendaStatus.FiadoAberto : VendaStatus.Pago;
    }

    /// <summary>
    /// Marca uma venda fiado como quitada (usada quando um pagamento do cliente abate o saldo
    /// devedor). Não tem efeito em vendas que não sejam fiado.
    /// </summary>
    public void MarcarFiadoQuitado()
    {
        if (FormaPagamento == FormaPagamento.Fiado && Status == VendaStatus.FiadoAberto)
        {
            Status = VendaStatus.FiadoQuitado;
        }
    }

    public ItemVenda AdicionarItem(Guid produtoId, int quantidade, decimal precoUnitario, decimal precoCustoUnitario)
    {
        var item = new ItemVenda(Id, produtoId, quantidade, precoUnitario, precoCustoUnitario);
        _itens.Add(item);
        RatearDesconto();
        return item;
    }

    /// <summary>
    /// Define (ou substitui) o desconto único da venda, começando em zero, e recalcula a
    /// parcela rateada proporcionalmente ao valor de cada item.
    /// </summary>
    public void AplicarDesconto(decimal desconto)
    {
        if (desconto < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(desconto), "O desconto não pode ser negativo.");
        }

        if (desconto > ValorBruto)
        {
            throw new ArgumentOutOfRangeException(nameof(desconto), "O desconto não pode ser maior que o valor bruto da venda.");
        }

        Desconto = desconto;
        RatearDesconto();
    }

    private void RatearDesconto()
    {
        if (_itens.Count == 0)
        {
            return;
        }

        var valorBruto = ValorBruto;
        if (valorBruto == 0)
        {
            foreach (var item in _itens)
            {
                item.AplicarDescontoRateado(0m);
            }

            return;
        }

        decimal acumulado = 0m;
        for (var i = 0; i < _itens.Count; i++)
        {
            var item = _itens[i];
            decimal parcela;
            if (i == _itens.Count - 1)
            {
                // O último item absorve o resíduo de arredondamento, para o total bater exatamente.
                parcela = Desconto - acumulado;
            }
            else
            {
                parcela = Math.Round(Desconto * (item.ValorTotal / valorBruto), 2, MidpointRounding.AwayFromZero);
                acumulado += parcela;
            }

            item.AplicarDescontoRateado(parcela);
        }
    }
}
