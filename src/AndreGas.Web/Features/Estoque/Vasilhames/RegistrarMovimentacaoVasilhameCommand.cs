using AndreGas.Domain.Enums;

using Mediator;

namespace AndreGas.Web.Features.Estoque.Vasilhames;

/// <summary>
/// Registra uma movimentação de vasilhames <b>disponíveis</b> (vazios) de um produto que controla
/// vasilhame: <see cref="TipoMovimentacaoEstoque.Entrada"/> soma, <see cref="TipoMovimentacaoEstoque.Saida"/>
/// subtrai (validando disponíveis) e <see cref="TipoMovimentacaoEstoque.Ajuste"/> define o total.
/// Não mexe nos vasilhames emprestados (<c>QuantidadeVasilhamesEmprestados</c>).
/// </summary>
public sealed record RegistrarMovimentacaoVasilhameCommand(
    Guid ProdutoId,
    TipoMovimentacaoEstoque Tipo,
    int Quantidade,
    string? Observacao = null) : ICommand<bool>;