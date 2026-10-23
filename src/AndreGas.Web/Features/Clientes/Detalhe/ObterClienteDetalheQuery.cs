using AndreGas.Domain.Enums;
using AndreGas.Web.Features.Clientes.Emprestimos;

using Mediator;

namespace AndreGas.Web.Features.Clientes.Detalhe;

/// <summary>Busca os dados de um cliente para exibição/edição, incluindo saldo devedor e
/// histórico de compras.</summary>
public sealed record ObterClienteDetalheQuery(Guid ClienteId) : IQuery<ClienteDetalheResult?>;

public sealed record ClienteDetalheResult(
    Guid Id,
    string Nome,
    string Telefone,
    string Endereco,
    decimal SaldoDevedor,
    bool Ativo,
    IReadOnlyList<VendaHistoricoItem> HistoricoDeCompras,
    IReadOnlyList<PagamentoHistoricoItem> HistoricoDePagamentos,
    IReadOnlyList<EmprestimoClienteItem> Emprestimos,
    IReadOnlyList<ProdutoCompradoItem> ProdutosComprados,
    IReadOnlyList<RecompraClienteItem> Recompras);

public sealed record VendaHistoricoItem(
    Guid VendaId,
    DateTime DataHora,
    FormaPagamento FormaPagamento,
    VendaStatus Status,
    decimal ValorTotal,
    decimal LucroTotal,
    decimal ValorEntrega);

public sealed record PagamentoHistoricoItem(
    Guid PagamentoId,
    DateTime Data,
    decimal Valor,
    FormaPagamento FormaPagamento,
    string? Observacao);

/// <summary>Total de unidades de cada produto compradas pelo cliente, agregado por produto.</summary>
public sealed record ProdutoCompradoItem(
    Guid ProdutoId,
    string ProdutoNome,
    int QuantidadeTotal);

/// <summary>
/// Recompra atual de um produto com <see cref="AndreGas.Domain.Entities.Produto.AvisarRecompra"/>
/// para este cliente: intervalo aprendido (dias) e data prevista da próxima compra (dia de Brasília).
/// <see cref="DiasDecorridos"/> é quantos dias se passaram desde a data prevista (negativo = ainda
/// não venceu).
/// </summary>
public sealed record RecompraClienteItem(
    Guid ProdutoId,
    string ProdutoNome,
    DateTime UltimaCompra,
    decimal IntervaloMedioDias,
    DateTime DataPrevista,
    int DiasDecorridos,
    int QuantidadeCompras);