using Mediator;

namespace RevendaApp.Web.Features.Estoque.Vasilhames;

/// <summary>
/// Lista o controle de vasilhames da revendedora: apenas produtos que <c>ControlaVasilhame</c>,
/// cada um com disponíveis/emprestados/total e a lista de clientes com saldo em aberto ("para quem").
/// </summary>
public sealed record ObterControleVasilhamesQuery : IQuery<IReadOnlyList<ControleVasilhameItem>>;

public sealed record ControleVasilhameItem(
    Guid ProdutoId,
    string Nome,
    int Disponivel,
    int Emprestados,
    int Total,
    IReadOnlyList<EmprestimoParaClienteItem> EmprestimosPara);

/// <summary>
/// Cliente com vasilhames do produto emprestados em aberto (<c>Saldo &gt; 0</c>), consolidado na
/// quantidade total. Equivale no "para quem" ao que <c>EmprestimoClienteItem</c> representa no
/// detalhe do cliente — aqui o cliente (não o produto) é a chave do agrupamento.
/// </summary>
public sealed record EmprestimoParaClienteItem(
    Guid ClienteId,
    string ClienteNome,
    int Quantidade);