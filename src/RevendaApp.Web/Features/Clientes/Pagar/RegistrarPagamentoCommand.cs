using Mediator;

using RevendaApp.Domain.Enums;

namespace RevendaApp.Web.Features.Clientes.Pagar;

/// <summary>
/// Registra um pagamento de um cliente, abatendo o saldo devedor (fiado) e quitando as
/// vendas fiado mais antigas proporcionalmente ao valor pago.
/// </summary>
public sealed record RegistrarPagamentoCommand(
    Guid ClienteId,
    decimal Valor,
    FormaPagamento FormaPagamento,
    string? Observacao = null) : ICommand<RegistrarPagamentoResult>;

public sealed record RegistrarPagamentoResult(Guid PagamentoId, decimal SaldoDevedorRestante);