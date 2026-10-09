namespace AndreGas.Domain.Entities;

/// <summary>
/// Preço negociado entre um cliente e um produto (acordo). Quando existe, a tela de nova venda
/// sugere este valor em vez do preço padrão do produto; na venda, o operador pode alterá-lo e o
/// acordo é atualizado (ou removido quando volta ao preço padrão). Não se aplica à forma de
/// pagamento "Gás do Povo", que usa sempre o preço subsidiado do produto.
/// </summary>
public class PrecoPersonalizadoCliente
{
    public Guid Id { get; private set; }
    public Guid ClienteId { get; private set; }
    public Cliente? Cliente { get; private set; }
    public Guid ProdutoId { get; private set; }
    public Produto? Produto { get; private set; }

    /// <summary>Preço negociado para venda deste produto a este cliente.</summary>
    public decimal Preco { get; private set; }

    public DateTime CriadoEm { get; private set; }

    /// <summary>Construtor exigido pelo EF Core.</summary>
    private PrecoPersonalizadoCliente()
    {
    }

    public PrecoPersonalizadoCliente(Guid clienteId, Guid produtoId, decimal preco)
    {
        if (preco <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(preco), "O preço personalizado deve ser maior que zero.");
        }

        Id = Guid.NewGuid();
        ClienteId = clienteId;
        ProdutoId = produtoId;
        Preco = preco;
        CriadoEm = DateTime.UtcNow;
    }

    public void AtualizarPreco(decimal preco)
    {
        if (preco <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(preco), "O preço personalizado deve ser maior que zero.");
        }

        Preco = preco;
    }
}