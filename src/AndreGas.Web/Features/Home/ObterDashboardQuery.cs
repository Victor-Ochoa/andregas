using Mediator;

namespace AndreGas.Web.Features.Home;

/// <summary>Agrega os dados exibidos no dashboard da tela inicial.</summary>
public sealed record ObterDashboardQuery : IQuery<DashboardResult>;

public sealed record DashboardResult(
    decimal VendasHojeTotal,
    int VendasHojeQuantidade,
    decimal VendasMesTotal,
    int VendasMesQuantidade,
    decimal LucroHoje,
    decimal LucroMes,
    IReadOnlyList<VendaDiaria> VendasPorDiaNoMes,
    IReadOnlyList<ProdutoEstoqueBaixoItem> ProdutosEstoqueBaixo);

public sealed record VendaDiaria(DateOnly Data, decimal Total, decimal Lucro);

public sealed record ProdutoEstoqueBaixoItem(Guid Id, string Nome, int QuantidadeEstoque, int EstoqueMinimo);
