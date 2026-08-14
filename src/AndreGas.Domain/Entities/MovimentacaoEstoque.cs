using AndreGas.Domain.Enums;

namespace AndreGas.Domain.Entities;

/// <summary>
/// Registro de entrada, saída ou ajuste de estoque de um produto.
/// </summary>
public class MovimentacaoEstoque
{
    public Guid Id { get; private set; }
    public Guid ProdutoId { get; private set; }
    public Produto? Produto { get; private set; }
    public TipoMovimentacaoEstoque Tipo { get; private set; }
    public int Quantidade { get; private set; }
    public DateTime Data { get; private set; }
    public string? Motivo { get; private set; }

    private MovimentacaoEstoque()
    {
    }

    public MovimentacaoEstoque(Guid produtoId, TipoMovimentacaoEstoque tipo, int quantidade, string? motivo = null, DateTime? data = null)
    {
        if (quantidade <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantidade), "A quantidade movimentada deve ser maior que zero.");
        }

        Id = Guid.NewGuid();
        ProdutoId = produtoId;
        Tipo = tipo;
        Quantidade = quantidade;
        Motivo = motivo;
        Data = data ?? DateTime.UtcNow;
    }
}
