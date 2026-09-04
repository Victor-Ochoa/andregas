using AndreGas.Domain.Enums;
using Mediator;

namespace AndreGas.Web.Features.Clientes.Detalhe;

/// <summary>Busca os dados de um cliente para exibição/edição, incluindo saldo devedor e
/// histórico de compras.</summary>
public sealed record ObterClienteDetalheQuery(Guid ClienteId) : IQuery<ClienteDetalheResult?>;

public sealed record ClienteDetalheResult(
    Guid Id,
    string Nome,
    string Telefone,
    string Endereco,
    decimal SaldoDevedor,
    bool Ativo,
    IReadOnlyList<VendaHistoricoItem> HistoricoDeCompras,
    IReadOnlyList<PagamentoHistoricoItem> HistoricoDePagamentos);

public sealed record VendaHistoricoItem(
    Guid VendaId,
    DateTime DataHora,
    FormaPagamento FormaPagamento,
    VendaStatus Status,
    decimal ValorTotal,
    decimal LucroTotal);

public sealed record PagamentoHistoricoItem(
    Guid PagamentoId,
    DateTime Data,
    decimal Valor,
    FormaPagamento FormaPagamento,
    string? Observacao);
