using AndreGas.Domain.Enums;

namespace AndreGas.Domain.Entities;

/// <summary>
/// Produto vendido pela revendedora (botijões de gás, água mineral, etc.).
/// </summary>
public class Produto
{
    public Guid Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public TipoProduto Tipo { get; private set; }
    public decimal PrecoVenda { get; private set; }
    public decimal PrecoCusto { get; private set; }

    /// <summary>Preço subsidiado usado quando a forma de pagamento é "Gás do Povo".</summary>
    public decimal PrecoGasDoPovo { get; private set; }

    public int QuantidadeEstoque { get; private set; }
    public int EstoqueMinimo { get; private set; }

    public bool EstoqueBaixo => QuantidadeEstoque <= EstoqueMinimo;

    private Produto()
    {
    }

    public Produto(string nome, TipoProduto tipo, decimal precoVenda, decimal precoCusto, decimal precoGasDoPovo, int estoqueMinimo = 0)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("O nome do produto é obrigatório.", nameof(nome));
        }

        if (precoVenda < 0 || precoCusto < 0 || precoGasDoPovo < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(precoVenda), "Os preços não podem ser negativos.");
        }

        if (estoqueMinimo < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(estoqueMinimo), "O estoque mínimo não pode ser negativo.");
        }

        Id = Guid.NewGuid();
        Nome = nome.Trim();
        Tipo = tipo;
        PrecoVenda = precoVenda;
        PrecoCusto = precoCusto;
        PrecoGasDoPovo = precoGasDoPovo;
        EstoqueMinimo = estoqueMinimo;
        QuantidadeEstoque = 0;
    }

    /// <summary>Preço unitário a aplicar de acordo com a forma de pagamento da venda.</summary>
    public decimal PrecoParaFormaPagamento(FormaPagamento formaPagamento) =>
        formaPagamento == FormaPagamento.GasDoPovo ? PrecoGasDoPovo : PrecoVenda;

    public void AtualizarDados(string nome, TipoProduto tipo, decimal precoVenda, decimal precoCusto, decimal precoGasDoPovo, int estoqueMinimo)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("O nome do produto é obrigatório.", nameof(nome));
        }

        if (precoVenda < 0 || precoCusto < 0 || precoGasDoPovo < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(precoVenda), "Os preços não podem ser negativos.");
        }

        Nome = nome.Trim();
        Tipo = tipo;
        PrecoVenda = precoVenda;
        PrecoCusto = precoCusto;
        PrecoGasDoPovo = precoGasDoPovo;
        EstoqueMinimo = estoqueMinimo;
    }

    public void RegistrarEntrada(int quantidade)
    {
        if (quantidade <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantidade), "A quantidade de entrada deve ser maior que zero.");
        }

        QuantidadeEstoque += quantidade;
    }

    public void RegistrarSaida(int quantidade)
    {
        if (quantidade <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantidade), "A quantidade de saída deve ser maior que zero.");
        }

        if (quantidade > QuantidadeEstoque)
        {
            throw new InvalidOperationException($"Estoque insuficiente de '{Nome}': disponível {QuantidadeEstoque}, solicitado {quantidade}.");
        }

        QuantidadeEstoque -= quantidade;
    }

    public void AjustarEstoque(int novaQuantidade)
    {
        if (novaQuantidade < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(novaQuantidade), "A quantidade em estoque não pode ser negativa.");
        }

        QuantidadeEstoque = novaQuantidade;
    }
}
