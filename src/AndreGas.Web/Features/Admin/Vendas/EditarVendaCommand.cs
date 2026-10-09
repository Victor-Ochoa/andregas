using AndreGas.Domain.Enums;
using AndreGas.Web.Features.Vendas.NovaVenda;

using Mediator;

namespace AndreGas.Web.Features.Admin.Vendas;

/// <summary>
/// Edita uma venda existente, substituindo seus itens/formas/valores. Reverte o impacto da venda
/// original (saldo devedor de fiado e estoque) e re-cria a partir do estado novo via
/// <see cref="RegistrarVendaCommand"/>, preservando preços personalizados e regras de Gás do Povo.
/// </summary>
public sealed record EditarVendaCommand(
    Guid VendaId,
    FormaPagamento FormaPagamento,
    decimal Desconto,
    IReadOnlyList<ItemVendaInput> Itens,
    decimal ValorEntrega = 0m) : ICommand<EditarVendaResult>;

public sealed record EditarVendaResult(
    Guid VendaId,
    Guid ClienteId,
    decimal ValorTotal,
    decimal LucroTotal,
    decimal SaldoDevedor);