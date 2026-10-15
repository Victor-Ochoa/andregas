using Mediator;

namespace AndreGas.Web.Features.Estoque.Vasilhames;

/// <summary>
/// Lista os clientes que têm um <b>empréstimo aberto</b> (saldo &gt; 0) daquele produto de
/// vasilhame — usada na modal de devolução para oferecer apenas quem pode devolver.
/// </summary>
public sealed record ObterClientesComEmprestimoProdutoQuery(Guid ProdutoId)
    : IQuery<IReadOnlyList<ClienteComEmprestimoItem>>;

public sealed record ClienteComEmprestimoItem(
    Guid ClienteId,
    string ClienteNome,
    string Telefone,
    int QuantidadeEmprestada);