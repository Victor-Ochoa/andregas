using AndreGas.Domain.Enums;

using Mediator;

namespace AndreGas.Web.Features.Estoque.Cadastrar;

/// <summary>
/// Cadastra um novo produto no catálogo (estoque inicial sempre zero).
/// Quando <see cref="ControlaVasilhame"/> é true, <see cref="QuantidadeVasilhamesInicial"/>
/// define quantos vasilhames vazios já entram disponíveis no depósito no cadastro.
/// </summary>
public sealed record CadastrarProdutoCommand(
    string Nome,
    TipoProduto Tipo,
    decimal PrecoVenda,
    decimal PrecoCusto,
    decimal PrecoGasDoPovo,
    int EstoqueMinimo,
    bool ControlaVasilhame = false,
    int QuantidadeVasilhamesInicial = 0,
    bool AvisarRecompra = false,
    int DiasPadraoRecompra = 0) : ICommand<Guid>;