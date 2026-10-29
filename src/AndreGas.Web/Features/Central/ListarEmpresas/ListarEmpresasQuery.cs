using Mediator;

namespace AndreGas.Web.Features.Central.ListarEmpresas;

/// <summary>
/// Lista as empresas cadastradas para a central do super administrador, com busca opcional por
/// nome/identificador e filtro de status.
/// </summary>
/// <param name="Busca">Trecho de nome ou identificador (parcial, case-insensitive). Nulo/vazio = sem filtro.</param>
/// <param name="Ativo">Filtra por status; <c>null</c> = ativas e inativas.</param>
public sealed record ListarEmpresasQuery(string? Busca = null, bool? Ativo = null) : IQuery<IReadOnlyList<EmpresaResumo>>;

/// <summary>
/// Linha da listagem de empresas da central. <see cref="TotalUsuarios"/> é contado numa única
/// consulta agrupada (sem N+1) e <see cref="TemLogo"/> evita trafegar o base64 (~500KB) para a grade.
/// </summary>
public sealed record EmpresaResumo(
    Guid Id,
    string Nome,
    string Identificador,
    bool Ativo,
    string? Cidade,
    string? Estado,
    int TotalUsuarios,
    string? CorPrimaria,
    bool TemLogo);