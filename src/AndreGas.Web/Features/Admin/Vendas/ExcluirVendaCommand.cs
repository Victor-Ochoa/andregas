using Mediator;

namespace AndreGas.Web.Features.Admin.Vendas;

/// <summary>
/// Exclui (logicamente) uma venda: estorna o saldo devedor (fiado) e o estoque, e marca a venda
/// como excluída (<see cref="AndreGas.Domain.Entities.Venda.Excluir"/>), mantendo o registro no
/// banco apenas para não quebrar referências. Vendas fiado já quitadas (saldo não cobre) são
/// bloqueadas.
/// </summary>
public sealed record ExcluirVendaCommand(Guid VendaId) : ICommand<ExcluirVendaResult>;

public sealed record ExcluirVendaResult(Guid VendaId, bool Sucesso);