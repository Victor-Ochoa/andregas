using Mediator;

namespace RevendaApp.Web.Features.Clientes.Listar;

/// <summary>
/// Lista clientes de forma paginada para a tela de Clientes, com busca por termo
/// (nome, telefone ou endereço — parcial e case-insensitive) e filtros opcionais de
/// status (ativo/inativo), saldo devedor e vasilhame emprestado. Filtros nulos/não
/// informados não restringem a consulta; <see cref="Termo"/> em branco é ignorado.
/// </summary>
public sealed record ListarClientesPaginadoQuery(
    string? Termo,
    bool? Ativo,
    bool? ComSaldoDevedor,
    bool? ComEmprestimo,
    int Pagina = 1,
    int TamanhoPagina = 20) : IQuery<ClienteListaPaginada>;

/// <summary>Resultado paginado da listagem de clientes.</summary>
public sealed record ClienteListaPaginada(
    IReadOnlyList<ClienteListItem> Itens,
    int Total,
    int TotalPaginas);