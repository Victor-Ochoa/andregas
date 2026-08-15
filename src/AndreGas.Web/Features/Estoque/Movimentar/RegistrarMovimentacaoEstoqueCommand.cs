using AndreGas.Domain.Enums;
using Mediator;

namespace AndreGas.Web.Features.Estoque.Movimentar;

/// <summary>Registra uma movimentação de estoque (entrada, saída ou ajuste) de um produto.</summary>
public sealed record RegistrarMovimentacaoEstoqueCommand(
    Guid ProdutoId,
    TipoMovimentacaoEstoque Tipo,
    int Quantidade,
    string? Motivo) : ICommand<bool>;
