using AndreGas.Domain.Enums;

using Mediator;

namespace AndreGas.Web.Features.Home;

/// <summary>
/// Obtém os dados de uma venda (cliente + itens com preço praticado) para exibição read-only
/// no modal de detalhe do dashboard. Só vendas não excluídas.
/// </summary>
public sealed record ObterVendaParaDetalheQuery(Guid VendaId) : IQuery<VendaParaDetalheResult?>;

public sealed record VendaParaDetalheResult(
    Guid VendaId,
    Guid ClienteId,
    string ClienteNome,
    string ClienteTelefone,
    FormaPagamento FormaPagamento,
    VendaStatus Status,
    decimal ValorTotal,
    decimal LucroTotal,
    decimal Desconto,
    decimal ValorEntrega,
    DateTime DataHora,
    IReadOnlyList<VendaItemParaDetalhe> Itens);

public sealed record VendaItemParaDetalhe(
    Guid ProdutoId,
    string ProdutoNome,
    int Quantidade,
    decimal PrecoUnitario,
    decimal PrecoCustoUnitario,
    bool EhEmprestimo);