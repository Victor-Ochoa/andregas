using Mediator;

namespace AndreGas.Web.Features.Clientes.Listar;

/// <summary>Lista todos os clientes cadastrados, com o saldo devedor atual de cada um.</summary>
public sealed record ListarClientesQuery : IQuery<IReadOnlyList<ClienteListItem>>;

public sealed record ClienteListItem(Guid Id, string Nome, string Telefone, string Endereco, decimal SaldoDevedor, bool Ativo);