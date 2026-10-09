using AndreGas.Domain.Enums;

using Mediator;

namespace AndreGas.Web.Features.Admin.Vendas;

/// <summary>Lista as vendas (não excluídas) para a gestão administrativa na tela de Configurações.</summary>
public sealed record ListarVendasQuery : IQuery<IReadOnlyList<VendaAdminListItem>>;

public sealed record VendaAdminListItem(
    Guid Id,
    DateTime DataHora,
    string ClienteNome,
    string? ClienteTelefone,
    FormaPagamento FormaPagamento,
    VendaStatus Status,
    decimal ValorTotal,
    decimal LucroTotal,
    decimal Desconto,
    decimal ValorEntrega,
    int QuantidadeItens,
    string? VendedorNome);