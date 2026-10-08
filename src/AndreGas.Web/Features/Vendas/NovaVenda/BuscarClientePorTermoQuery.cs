using Mediator;

namespace AndreGas.Web.Features.Vendas.NovaVenda;

/// <summary>
/// Busca clientes pelo termo (nome, telefone ou endereço — parciais e case-insensitive) para o
/// autocomplete da tela de nova venda. Retorna vazio quando o termo é nulo ou em branco.
/// </summary>
public sealed record BuscarClientePorTermoQuery(string Termo) : IQuery<IReadOnlyList<ClienteSugestao>>;

/// <summary>
/// Cliente sugerido na busca, com o valor de entrega da última venda (0 se ainda não houve venda)
/// para sugerir na tela de nova venda.
/// </summary>
public sealed record ClienteSugestao(
    Guid Id,
    string Nome,
    string Telefone,
    string Endereco,
    decimal SaldoDevedor,
    decimal UltimaValorEntrega);