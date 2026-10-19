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
    IReadOnlyList<PagamentoDetalhe> Pagamentos,
    IReadOnlyList<VendaPorFormaItem> VendasPorForma,
    IReadOnlyList<TopProdutoItem> TopProdutos,
    IReadOnlyList<TopDevedorItem> TopDevedores,
    IReadOnlyList<VasilhameDashboardItem> Vasilhames);

/// <summary>
/// Estado físico de um produto para o gráfico da Home: cheios em estoque (<see cref="Estoque"/>),
/// vasilhames vazios disponíveis no depósito (<see cref="Vasilhame"/>) e vasilhames emprestados a
/// clientes (<see cref="Emprestado"/>). Cobre <b>todos</b> os produtos, inclusive os sem controle
/// de vasilhame (que ficam com Vasilhame e Emprestado zerados) e os inativos.
/// </summary>
public sealed record VasilhameDashboardItem(
    Guid ProdutoId,
    string Nome,
    int Estoque,
    int Vasilhame,
    int Emprestado);

public sealed record VendaPeriodo(string Rotulo, decimal Total, decimal Lucro);

public sealed record ProdutoEstoqueBaixoItem(Guid Id, string Nome, int QuantidadeEstoque, int EstoqueMinimo);

/// <summary>Linha da tabela de vendas do dashboard, respeitando o período selecionado.</summary>
public sealed record VendaDetalhe(
    Guid VendaId,
    DateTime DataHora,
    string ClienteNome,
    string ClienteTelefone,
    FormaPagamento FormaPagamento,
    VendaStatus Status,
    int QuantidadeItens,
    decimal ValorTotal,
    decimal Lucro,
    decimal Desconto,
    decimal ValorEntrega,
    IReadOnlyList<Guid> ProdutoIds);

/// <summary>Linha da tabela de pagamentos do dashboard, respeitando o período selecionado.</summary>
public sealed record PagamentoDetalhe(
    DateTime Data,
    string ClienteNome,
    string ClienteTelefone,
    FormaPagamento FormaPagamento,
    decimal Valor,
    string? Observacao);

/// <summary>Total de vendas por forma de pagamento no período (para o gráfico donut).</summary>
public sealed record VendaPorFormaItem(FormaPagamento FormaPagamento, decimal Valor);

/// <summary>Produto mais vendido no período (por quantidade), com receita e lucro.</summary>
public sealed record TopProdutoItem(Guid ProdutoId, string Nome, int QuantidadeTotal, decimal Receita, decimal Lucro);

/// <summary>Cliente com saldo devedor em aberto (fiado), ordenado do maior para o menor.</summary>
public sealed record TopDevedorItem(Guid ClienteId, string Nome, decimal SaldoDevedor);