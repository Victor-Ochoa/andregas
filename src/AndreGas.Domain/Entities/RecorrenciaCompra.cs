namespace AndreGas.Domain.Entities;

/// <summary>
/// Resumo aprendido da recorrência de compra de um cliente para um produto (quando o produto tem
/// <see cref="Produto.AvisarRecompra"/>). Única linha por (Cliente, Produto); é recalculada a partir
/// das vendas vigentes sempre que o conjunto de vendas do par muda (nova venda, edição, exclusão),
/// por <c>RecorrenciaCompraAprendizado</c>. A data prevista de nova compra é derivada:
/// <c>DataUltimaCompra + IntervaloMedioDias</c>.
/// </summary>
public class RecorrenciaCompra
{
    public Guid Id { get; private set; }
    public Guid ClienteId { get; private set; }
    public Cliente? Cliente { get; private set; }
    public Guid ProdutoId { get; private set; }
    public Produto? Produto { get; private set; }

    /// <summary>Data da última venda vigente do produto para este cliente.</summary>
    public DateTime DataUltimaCompra { get; private set; }

    /// <summary>
    /// Intervalo médio (em dias) entre compras consecutivas do cliente para este produto. Com uma
    /// única compra equivale ao <see cref="Produto.DiasPadraoRecompra"/>; sem histórico suficiente,
    /// fica zerado (não gera aviso).
    /// </summary>
    public decimal IntervaloMedioDias { get; private set; }

    /// <summary>Número de vendas vigentes do par consideradas no aprendizado.</summary>
    public int QuantidadeCompras { get; private set; }

    /// <summary>Construtor exigido pelo EF Core.</summary>
    private RecorrenciaCompra()
    {
    }

    public RecorrenciaCompra(Guid clienteId, Guid produtoId, DateTime dataUltimaCompra, decimal intervaloMedioDias, int quantidadeCompras)
    {
        Id = Guid.NewGuid();
        ClienteId = clienteId;
        ProdutoId = produtoId;
        DataUltimaCompra = dataUltimaCompra;
        IntervaloMedioDias = intervaloMedioDias;
        QuantidadeCompras = quantidadeCompras;
    }

    /// <summary>Substitui o resumo calculado a partir das vendas vigentes do par.</summary>
    public void Atualizar(DateTime dataUltimaCompra, decimal intervaloMedioDias, int quantidadeCompras)
    {
        DataUltimaCompra = dataUltimaCompra;
        IntervaloMedioDias = intervaloMedioDias;
        QuantidadeCompras = quantidadeCompras;
    }
}