using Mediator;

using RevendaApp.Domain.Enums;

namespace RevendaApp.Web.Features.Estoque.Editar;

/// <summary>
/// Atualiza os dados de um produto existente (nome, tipo, preços e estoque mínimo) e o estado
/// ativo/inativo (usado pela modal de edição no estoque). Retorna false se o produto não existir.
/// </summary>
public sealed record AtualizarProdutoCommand(
    Guid ProdutoId,
    string Nome,
    TipoProduto Tipo,
    decimal PrecoVenda,
    decimal PrecoCusto,
    decimal PrecoGasDoPovo,
    int EstoqueMinimo,
    bool Ativo,
    bool ControlaVasilhame = false,
    bool AvisarRecompra = false,
    int DiasPadraoRecompra = 0) : ICommand<bool>;