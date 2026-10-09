using AndreGas.Domain.Enums;

using Mediator;

namespace AndreGas.Web.Features.Admin.Vendas;

/// <summary>
/// Obtém os dados atuais de uma venda (cliente + itens com preço praticado) para a tela de
/// edição. Só vendas não excluídas.
/// </summary>
public sealed record ObterVendaParaEdicaoQuery(Guid VendaId) : IQuery<ObterVendaParaEdicaoResult?>;

public sealed record ObterVendaParaEdicaoResult(
    Guid VendaId,
    Guid ClienteId,
    string ClienteNome,
    string? ClienteTelefone,
    FormaPagamento FormaPagamento,
    VendaStatus Status,
    decimal Desconto,
    decimal ValorEntrega,
    decimal SaldoDevedor,
    IReadOnlyList<VendaItemEdicao> Itens);

public sealed record VendaItemEdicao(
    Guid ProdutoId,
    string ProdutoNome,
    int Quantidade,
    decimal PrecoUnitario);