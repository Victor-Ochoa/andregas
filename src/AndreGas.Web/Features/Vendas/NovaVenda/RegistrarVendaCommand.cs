using AndreGas.Domain.Enums;
using Mediator;

namespace AndreGas.Web.Features.Vendas.NovaVenda;

/// <summary>Item de produto/quantidade escolhido na tela de nova venda.</summary>
public sealed record ItemVendaInput(Guid ProdutoId, int Quantidade);

/// <summary>
/// Registra uma nova venda. O cliente pode ser um já cadastrado, identificado por
/// <see cref="ClienteId"/>; ou um novo, usando <see cref="NomeClienteNovo"/>/
/// <see cref="TelefoneClienteNovo"/>/<see cref="EnderecoClienteNovo"/> quando não há cliente
/// selecionado (cadastro rápido na tela).
/// </summary>
public sealed record RegistrarVendaCommand(
    Guid? ClienteId,
    string? NomeClienteNovo,
    string? TelefoneClienteNovo,
    string? EnderecoClienteNovo,
    FormaPagamento FormaPagamento,
    decimal Desconto,
    IReadOnlyList<ItemVendaInput> Itens,
    decimal ValorEntrega = 0m) : ICommand<RegistrarVendaResult>;

public sealed record RegistrarVendaResult(Guid VendaId, Guid ClienteId, decimal ValorTotal, decimal LucroTotal);
