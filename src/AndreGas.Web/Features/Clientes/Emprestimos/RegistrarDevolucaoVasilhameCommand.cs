using Mediator;

namespace AndreGas.Web.Features.Clientes.Emprestimos;

/// <summary>
/// Registra a devolução <b>manual</b> de vasilhames emprestados a um cliente. A cascata é:
/// 1) quita do saldo em aberto do cliente (FIFO pelos empréstimos mais antigos);
/// 2) se o produto controla vasilhame e há estoque de cheio suficiente, reabastece o estoque
/// ("devolve o veículo cheio para vender de novo"); 3) senão, o estoque fica como está.
/// </summary>
public sealed record RegistrarDevolucaoVasilhameCommand(
    Guid ClienteId,
    Guid ProdutoId,
    int Quantidade,
    string? Observacao = null) : ICommand<bool>;