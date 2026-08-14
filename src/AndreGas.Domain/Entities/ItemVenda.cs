namespace AndreGas.Domain.Entities;

/// <summary>
/// Item de uma <see cref="Venda"/>: um produto, quantidade e os preços praticados no
/// momento da venda (snapshot), usados para calcular o lucro.
/// </summary>
public class ItemVenda
{
    public Guid Id { get; private set; }
    public Guid VendaId { get; private set; }
    public Guid ProdutoId { get; private set; }
    public Produto? Produto { get; private set; }
    public int Quantidade { get; private set; }
    public decimal PrecoUnitario { get; private set; }
    public decimal PrecoCustoUnitario { get; private set; }

    /// <summary>Parcela do desconto da venda rateada proporcionalmente para este item.</summary>
    public decimal DescontoRateado { get; private set; }

    /// <summary>Lucro do item sem considerar o desconto da venda.</summary>
    public decimal LucroBruto => (PrecoUnitario - PrecoCustoUnitario) * Quantidade;

    /// <summary>Lucro líquido do item, já descontada a parcela rateada do desconto da venda.</summary>
    public decimal Lucro => LucroBruto - DescontoRateado;

    public decimal ValorTotal => PrecoUnitario * Quantidade;

    private ItemVenda()
    {
    }

    internal ItemVenda(Guid vendaId, Guid produtoId, int quantidade, decimal precoUnitario, decimal precoCustoUnitario)
    {
        if (quantidade <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantidade), "A quantidade deve ser maior que zero.");
        }

        if (precoUnitario < 0 || precoCustoUnitario < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(precoUnitario), "Os preços não podem ser negativos.");
        }

        Id = Guid.NewGuid();
        VendaId = vendaId;
        ProdutoId = produtoId;
        Quantidade = quantidade;
        PrecoUnitario = precoUnitario;
        PrecoCustoUnitario = precoCustoUnitario;
    }

    internal void AplicarDescontoRateado(decimal descontoRateado) => DescontoRateado = descontoRateado;
}
