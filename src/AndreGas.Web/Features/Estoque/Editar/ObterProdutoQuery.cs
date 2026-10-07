using AndreGas.Domain.Enums;
using Mediator;

namespace AndreGas.Web.Features.Estoque.Editar;

/// <summary>Busca os dados de um produto para preencher a modal de edição do estoque.</summary>
public sealed record ObterProdutoQuery(Guid ProdutoId) : IQuery<ObterProdutoResult?>;

public sealed record ObterProdutoResult(
    Guid Id,
    string Nome,
    TipoProduto Tipo,
    decimal PrecoVenda,
    decimal PrecoCusto,
    decimal PrecoGasDoPovo,
    int EstoqueMinimo,
    bool Ativo);