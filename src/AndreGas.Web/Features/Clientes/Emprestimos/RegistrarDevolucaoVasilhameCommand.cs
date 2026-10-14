using Mediator;

namespace AndreGas.Web.Features.Clientes.Emprestimos;

/// <summary>
/// Registra a devolução <b>manual</b> de vasilhames emprestados a um cliente. A cascata é:
/// 1) quita do saldo em aberto do cliente (FIFO pelos empréstimos mais antigos);
/// 2) devolve o VASILHAME vazio ao depósito de vazios disponíveis (Emprestados− / Disponíveis+);
/// não mexe no Estoque de cheios.
/// </summary>
public sealed record RegistrarDevolucaoVasilhameCommand(
    Guid ClienteId,
    Guid ProdutoId,
    int Quantidade,
    string? Observacao = null) : ICommand<bool>;