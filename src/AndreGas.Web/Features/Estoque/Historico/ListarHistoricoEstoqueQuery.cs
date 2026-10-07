using AndreGas.Domain.Enums;
using Mediator;

namespace AndreGas.Web.Features.Estoque.Historico;

/// <summary>Busca o histórico de auditoria de estoque de um produto (mais recente primeiro).</summary>
public sealed record ListarHistoricoEstoqueQuery(Guid ProdutoId) : IQuery<IReadOnlyList<HistoricoEstoqueItem>>;

public sealed record HistoricoEstoqueItem(
    Guid ProdutoId,
    DateTime Data,
    TipoHistoricoEstoque Tipo,
    string? Descricao,
    string? Motivo,
    string Usuario,
    int? Quantidade,
    Guid? VendaId);