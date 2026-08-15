using Mediator;

namespace AndreGas.Web.Features.Vendas.NovaVenda;

/// <summary>Busca um cliente pelo telefone (chave natural) para a tela de nova venda.</summary>
public sealed record BuscarClientePorTelefoneQuery(string Telefone) : IQuery<ClienteEncontrado?>;

public sealed record ClienteEncontrado(Guid Id, string Nome, string Endereco, decimal SaldoDevedor);
