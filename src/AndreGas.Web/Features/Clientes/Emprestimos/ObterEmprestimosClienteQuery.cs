using Mediator;

namespace AndreGas.Web.Features.Clientes.Emprestimos;

/// <summary>
/// Lista os empréstimos de vasilhame <b>em aberto</b> (DevolvidoEm nulo) de um cliente,
/// agrupados por produto. Quando o mesmo produto tem vários empréstimos abertos, o item
/// consolida a quantidade (saldo em aberto total) e a data do empréstimo mais antigo.
/// </summary>
public sealed record ObterEmprestimosClienteQuery(Guid ClienteId) : IQuery<IReadOnlyList<EmprestimoClienteItem>>;

public sealed record EmprestimoClienteItem(
    Guid ProdutoId,
    string ProdutoNome,
    int Quantidade,
    DateTime Data);