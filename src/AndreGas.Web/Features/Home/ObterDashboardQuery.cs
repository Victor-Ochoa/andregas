using AndreGas.Domain.Enums;

using Mediator;

namespace AndreGas.Web.Features.Home;

/// <summary>
/// Agrega os dados exibidos no dashboard da home, filtrados por uma janela de tempo
/// (<see cref="PeriodoDashboard"/>).
/// </summary>
public sealed record ObterDashboardQuery(PeriodoDashboard Periodo = PeriodoDashboard.Hoje) : IQuery<DashboardResult>;

public sealed record DashboardResult(
    PeriodoDashboard Periodo,
    decimal VendasTotal,
    int VendasQuantidade,
    decimal LucroTotal,
    decimal DescontoTotal,
    decimal TotalDevedor,
    IReadOnlyList<VendaPeriodo> VendasPorPeriodo,
    IReadOnlyList<ProdutoEstoqueBaixoItem> ProdutosEstoqueBaixo,
    IReadOnlyList<VendaDetalhe> Vendas,
    IReadOnlyList<PagamentoDetalhe> Pagamentos);

public sealed record VendaPeriodo(string Rotulo, decimal Total, decimal Lucro);

public sealed record ProdutoEstoqueBaixoItem(Guid Id, string Nome, int QuantidadeEstoque, int EstoqueMinimo);

/// <summary>Linha da tabela de vendas do dashboard, respeitando o período selecionado.</summary>
public sealed record VendaDetalhe(
    DateTime DataHora,
    string ClienteNome,
    string ClienteTelefone,
    FormaPagamento FormaPagamento,
    VendaStatus Status,
    int QuantidadeItens,
    decimal ValorTotal,
    decimal Lucro,
    decimal Desconto,
    decimal ValorEntrega);

/// <summary>Linha da tabela de pagamentos do dashboard, respeitando o período selecionado.</summary>
public sealed record PagamentoDetalhe(
    DateTime Data,
    string ClienteNome,
    string ClienteTelefone,
    FormaPagamento FormaPagamento,
    decimal Valor,
    string? Observacao);