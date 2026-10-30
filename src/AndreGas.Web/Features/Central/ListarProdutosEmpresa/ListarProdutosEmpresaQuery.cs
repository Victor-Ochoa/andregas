using AndreGas.Domain.Enums;

using Mediator;

namespace AndreGas.Web.Features.Central.ListarProdutosEmpresa;

/// <summary>
/// Lista o catálogo de uma empresa na central — <b>somente leitura</b>. Criar, editar ou excluir
/// produtos de uma empresa é feito pelo "Entrar como" (o super admin assume a sessão do admin dela).
/// </summary>
/// <param name="EmpresaId">Empresa cujo catálogo será exibido.</param>
public sealed record ListarProdutosEmpresaQuery(Guid EmpresaId) : IQuery<IReadOnlyList<ProdutoEmpresaItem>>;

/// <summary>Linha da listagem de produtos de uma empresa na central.</summary>
public sealed record ProdutoEmpresaItem(
    Guid Id,
    string Nome,
    TipoProduto Tipo,
    decimal PrecoVenda,
    int QuantidadeEstoque,
    bool Ativo);