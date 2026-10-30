using Mediator;

namespace AndreGas.Web.Features.Central.Auditoria;

/// <summary>
/// Lista paginada da auditoria para a central, mais recente primeiro, com filtros opcionais de
/// empresa, ação e período.
/// </summary>
/// <param name="EmpresaId">
/// Empresa a filtrar. <c>null</c> = todas (visão central, incluindo as ações sem empresa).
/// </param>
/// <param name="Acao">Ação exata a filtrar (constante de <c>RegistroAuditoria.Acoes</c>); nulo/vazio = todas.</param>
/// <param name="De">Início do período (data em horário de Brasília, inclusive).</param>
/// <param name="Ate">Fim do período (data em horário de Brasília, inclusive — o dia inteiro).</param>
/// <param name="Pagina">Página (1-based).</param>
/// <param name="TamanhoPagina">Itens por página (limitado a 100).</param>
public sealed record ListarAuditoriaQuery(
    Guid? EmpresaId = null,
    string? Acao = null,
    DateTime? De = null,
    DateTime? Ate = null,
    int Pagina = 1,
    int TamanhoPagina = 20) : IQuery<AuditoriaPaginada>;

/// <summary>Resultado paginado da auditoria.</summary>
/// <param name="Itens">Linhas da página atual.</param>
/// <param name="Total">Total de registros que casam com os filtros (não só da página).</param>
/// <param name="TamanhoPagina">Tamanho de página efetivamente usado (já limitado).</param>
public sealed record AuditoriaPaginada(IReadOnlyList<AuditoriaItem> Itens, int Total, int TamanhoPagina)
{
    /// <summary>Total de páginas (0 quando não há registros).</summary>
    public int TotalPaginas => Total == 0 ? 0 : (int)Math.Ceiling(Total / (double)TamanhoPagina);
}

/// <summary>
/// Linha da aba Auditoria. <see cref="EmpresaNome"/> já vem projetado na consulta (join único, sem
/// N+1) e é nulo para ações sem empresa.
/// </summary>
public sealed record AuditoriaItem(
    Guid Id,
    Guid? EmpresaId,
    string? EmpresaNome,
    Guid? UsuarioId,
    string UsuarioNome,
    string Acao,
    string Detalhes,
    DateTime DataHora,
    string Origem);