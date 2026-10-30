using Mediator;

using RevendaApp.Domain.Enums;

namespace RevendaApp.Web.Features.Estoque.Movimentar;

/// <summary>
/// Registra uma movimentação de estoque (entrada, saída ou ajuste) de um produto.
/// Em produtos que <see cref="Produto.ControlaVasilhame"/>, a entrada (compra) exige a entrega de
/// <c>Quantidade</c> vasilhames vazios ao fornecedor (Estoque +N e Vasilhames −N) e a saída
/// manual não mexe em vazios (as vendas tratam troca/empréstimo no RegistrarVendaCommandHandler).
/// </summary>
public sealed record RegistrarMovimentacaoEstoqueCommand(
    Guid ProdutoId,
    TipoMovimentacaoEstoque Tipo,
    int Quantidade,
    string? Motivo) : ICommand<bool>;