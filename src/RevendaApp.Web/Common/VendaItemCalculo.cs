using RevendaApp.Domain.Entities;
using RevendaApp.Domain.Enums;

namespace RevendaApp.Web.Common;

/// <summary>
/// Contrato mínimo de produto necessário para o cálculo de prévia de venda. Tanto a entidade
/// <see cref="Produto"/> quanto o DTO de listagem <c>ProdutoListItem</c> o implementam, então o
/// mesmo cálculo serve para os dialogs (que usam o DTO) e para os handlers (que usam a entidade).
/// </summary>
public interface IProdutoPreco
{
    Guid Id { get; }

    decimal PrecoVenda { get; }

    decimal PrecoGasDoPovo { get; }
}

/// <summary>
/// Cálculo de itens de venda que é compartilhado entre os dialogs de Nova Venda e Editar Venda.
/// Antes estes métodos estavam duplicados byte a byte nos dois dialogs e recalcularam a regra de
/// negócio "Gás do Povo usa o preço subsidiado" que já existe em
/// <see cref="Produto.PrecoParaFormaPagamento"/>. Centralizar aqui garante que a prévia de total
/// exibida ao operador nunca divirja do valor que o handler persiste.
/// </summary>
public static class VendaItemCalculo
{
    /// <summary>
    /// Preço a sugerir para o item: o acordo personalizado do cliente (se existir) ou o preço
    /// padrão do produto conforme a forma de pagamento.
    /// </summary>
    public static decimal PrecoSugeridoPara(
        Guid produtoId,
        IReadOnlyCollection<IProdutoPreco> produtos,
        IReadOnlyDictionary<Guid, decimal> precosPersonalizados,
        FormaPagamento formaPagamento)
    {
        if (precosPersonalizados.TryGetValue(produtoId, out var personalizado))
        {
            return personalizado;
        }

        return PrecoPadraoPara(produtoId, produtos, formaPagamento);
    }

    /// <summary>
    /// Preço padrão do produto conforme a forma de pagamento (Gás do Povo usa o preço subsidiado).
    /// Delega para <see cref="Produto.PrecoParaFormaPagamento"/> — a mesma regra do handler.
    /// </summary>
    public static decimal PrecoPadraoPara(
        Guid produtoId,
        IReadOnlyCollection<IProdutoPreco> produtos,
        FormaPagamento formaPagamento)
    {
        var produto = produtos.FirstOrDefault(p => p.Id == produtoId);
        if (produto is null)
        {
            return 0m;
        }

        return formaPagamento == FormaPagamento.GasDoPovo ? produto.PrecoGasDoPovo : produto.PrecoVenda;
    }

    /// <summary>
    /// Total bruto dos itens para a prévia do dialog: na forma Gás do Povo vale o preço
    /// subsidiado (não o preço editado/item); nas demais vale o preço exibido no item (que pode
    /// ser o acordo personalizado). O resultado é o bruto menos o desconto mais a entrega, sem
    /// nunca ficar negativo.
    /// </summary>
    public static decimal CalcularValorTotalPreview(
        IEnumerable<(Guid ProdutoId, decimal PrecoUnitario, int Quantidade)> itens,
        IReadOnlyCollection<IProdutoPreco> produtos,
        FormaPagamento formaPagamento,
        decimal desconto,
        decimal valorEntrega)
    {
        var bruto = itens.Sum(item =>
        {
            if (formaPagamento == FormaPagamento.GasDoPovo)
            {
                var produto = produtos.FirstOrDefault(p => p.Id == item.ProdutoId);
                return produto is null ? 0m : produto.PrecoGasDoPovo * item.Quantidade;
            }

            return item.PrecoUnitario * item.Quantidade;
        });

        return Math.Max(0m, bruto - desconto + valorEntrega);
    }
}