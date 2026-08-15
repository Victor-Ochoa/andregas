using AndreGas.Domain.Enums;
using Mediator;

namespace AndreGas.Web.Features.Estoque.Listar;

/// <summary>Lista todos os produtos com sua quantidade em estoque atual.</summary>
public sealed record ListarProdutosQuery : IQuery<IReadOnlyList<ProdutoListItem>>;

public sealed record ProdutoListItem(
    Guid Id,
    string Nome,
    TipoProduto Tipo,
    decimal PrecoVenda,
    decimal PrecoCusto,
    decimal PrecoGasDoPovo,
    int QuantidadeEstoque,
    int EstoqueMinimo,
    bool EstoqueBaixo);
