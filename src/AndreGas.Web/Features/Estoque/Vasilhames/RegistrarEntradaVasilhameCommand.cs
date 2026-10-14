using Mediator;

namespace AndreGas.Web.Features.Estoque.Vasilhames;

/// <summary>
/// Registra a entrada <b>manual</b> de vasilhames vazios no depósito de um produto (ex.: vazios
/// recolhidos fora do fluxo de devolução, ou reposição inicial de um produto novo com controle).
/// Incrementa o <c>QuantidadeVasilhames</c> (disponíveis) e grava histórico de estoque.
/// </summary>
public sealed record RegistrarEntradaVasilhameCommand(
    Guid ProdutoId,
    int Quantidade,
    string? Observacao = null) : ICommand<bool>;