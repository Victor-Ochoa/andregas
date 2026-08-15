using AndreGas.Domain.Enums;
using Mediator;

namespace AndreGas.Web.Features.Estoque.Cadastrar;

/// <summary>Cadastra um novo produto no catálogo (estoque inicial sempre zero).</summary>
public sealed record CadastrarProdutoCommand(
    string Nome,
    TipoProduto Tipo,
    decimal PrecoVenda,
    decimal PrecoCusto,
    decimal PrecoGasDoPovo,
    int EstoqueMinimo) : ICommand<Guid>;
