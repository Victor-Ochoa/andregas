using Mediator;

namespace AndreGas.Web.Features.Clientes.Emprestimos;

/// <summary>
/// Registra um empréstimo <b>manual</b> de vasilhames a um cliente (fora do fluxo de venda):
/// cria um <see cref="AndreGas.Domain.Entities.EmprestimoVasilhame"/> aberto e soma a quantidade
/// aos <c>QuantidadeVasilhamesEmprestados</c> do produto. Os vasilhames disponíveis não mudam —
/// o empréstimo apenas anota que o cliente está com eles.
/// </summary>
public sealed record RegistrarEmprestimoCommand(
    Guid ClienteId,
    Guid ProdutoId,
    int Quantidade,
    string? Observacao = null) : ICommand<Guid>;