using Mediator;

using RevendaApp.Domain.Enums;
using RevendaApp.Web.Common;

namespace RevendaApp.Web.Features.Estoque.Listar;

/// <summary>
/// Lista os produtos com sua quantidade em estoque atual. Por padrão retorna apenas produtos
/// ativos (ex.: para a nova venda); defina <see cref="IncluirInativos"/> para a tela de estoque,
/// que precisa exibir também os desabilitados.
/// </summary>
public sealed record ListarProdutosQuery(bool IncluirInativos = false) : IQuery<IReadOnlyList<ProdutoListItem>>;

public sealed record ProdutoListItem(
    Guid Id,
    string Nome,
    TipoProduto Tipo,
    decimal PrecoVenda,
    decimal PrecoCusto,
    decimal PrecoGasDoPovo,
    int QuantidadeEstoque,
    int EstoqueMinimo,
    bool EstoqueBaixo,
    bool Ativo,
    bool ControlaVasilhame,
    int QuantidadeVasilhames,
    int QuantidadeVasilhamesEmprestados,
    int TotalVasilhames)
    : IProdutoPreco
{
    /// <summary>
    /// Total de itens no pátio da revendedora: para produtos que controlam vasilhame conta os
    /// cheios (<see cref="QuantidadeEstoque"/>) somados aos vazios disponíveis
    /// (<see cref="QuantidadeVasilhames"/>); para os demais, apenas o estoque de cheios.
    /// </summary>
    public int Patio => ControlaVasilhame
        ? QuantidadeEstoque + QuantidadeVasilhames
        : QuantidadeEstoque;
}