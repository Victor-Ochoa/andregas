using AndreGas.Domain.Enums;
using Mediator;

namespace AndreGas.Web.Features.Vendas.NovaVenda;

/// <summary>Item de produto/quantidade escolhido na tela de nova venda.</summary>
public sealed record ItemVendaInput(Guid ProdutoId, int Quantidade);

/// <summary>
/// Registra uma nova venda. O cliente é localizado pelo telefone (chave natural); se não
/// existir, é cadastrado usando <see cref="NomeClienteNovo"/>/<see cref="EnderecoClienteNovo"/>.
/// </summary>
public sealed record RegistrarVendaCommand(
    string Telefone,
    string? NomeClienteNovo,
    string? EnderecoClienteNovo,
    FormaPagamento FormaPagamento,
    decimal Desconto,
    IReadOnlyList<ItemVendaInput> Itens,
    decimal ValorEntrega = 0m) : ICommand<RegistrarVendaResult>;

public sealed record RegistrarVendaResult(Guid VendaId, Guid ClienteId, decimal ValorTotal, decimal LucroTotal);
