using Mediator;

namespace RevendaApp.Web.Features.Clientes.Emprestimos;

/// <summary>
/// Registra um empréstimo <b>manual</b> de vasilhames a um cliente (fora do fluxo de venda):
/// pega um vasilhame vazio do depósito (disponível) e o entrega ao cliente (emprestado). Exige
/// vasilhame físico disponível — lança se não houver. Cria um
/// <see cref="RevendaApp.Domain.Entities.EmprestimoVasilhame"/> aberto, sai do
/// <c>QuantidadeVasilhames</c> e soma aos <c>QuantidadeVasilhamesEmprestados</c> do produto.
/// </summary>
public sealed record RegistrarEmprestimoCommand(
    Guid ClienteId,
    Guid ProdutoId,
    int Quantidade,
    string? Observacao = null) : ICommand<Guid>;