using Mediator;

namespace AndreGas.Web.Features.Vendas.NovaVenda;

/// <summary>Busca um cliente pelo telefone (chave natural) para a tela de nova venda.</summary>
public sealed record BuscarClientePorTelefoneQuery(string Telefone) : IQuery<ClienteEncontrado?>;

/// <summary>
/// Cliente encontrado pelo telefone, com o valor de entrega da última venda (0 se ainda não houve
/// venda) para sugerir na tela de nova venda.
/// </summary>
public sealed record ClienteEncontrado(Guid Id, string Nome, string Endereco, decimal SaldoDevedor, decimal UltimaValorEntrega);
