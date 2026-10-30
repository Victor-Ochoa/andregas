using Mediator;

using Microsoft.EntityFrameworkCore;

using RevendaApp.Domain.Entities;
using RevendaApp.Infrastructure;
using RevendaApp.Infrastructure.Tenancy;

namespace RevendaApp.Web.Features.Central.ListarEmpresas;

/// <summary>
/// Lista as empresas (tenants) para a central. Lê a tabela <c>Empresas</c>, que <b>não</b> tem
/// filtro global, e conta os usuários de cada empresa em <b>uma única</b> consulta agrupada —
/// nada de N+1 por linha.
/// </summary>
public sealed class ListarEmpresasQueryHandler(AppDbContext db, ITenantContext tenant)
    : IQueryHandler<ListarEmpresasQuery, IReadOnlyList<EmpresaResumo>>
{
    public async ValueTask<IReadOnlyList<EmpresaResumo>> Handle(ListarEmpresasQuery query, CancellationToken cancellationToken)
    {
        // Limite real de segurança: a política da rota não basta, o handler reforça no servidor.
        if (!tenant.EhSuperAdmin)
        {
            throw new InvalidOperationException("Somente o super administrador da central pode listar as empresas.");
        }

        var empresas = db.Set<Empresa>().AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Busca))
        {
            var padrao = $"%{EscapeLike(query.Busca.Trim())}%";
            empresas = empresas.Where(e =>
                EF.Functions.ILike(e.Nome, padrao, "\\")
                || EF.Functions.ILike(e.Identificador, padrao, "\\"));
        }

        if (query.Ativo is { } ativo)
        {
            empresas = empresas.Where(e => e.Ativo == ativo);
        }

        // Contagem de usuários por empresa numa só query (agrupada). O Identity não tem filtro
        // global por empresa, por isso o agrupamento explícito por EmpresaId.
        var totalPorEmpresa = await db.Users
            .Where(u => u.EmpresaId != null)
            .GroupBy(u => u.EmpresaId!.Value)
            .Select(g => new { EmpresaId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.EmpresaId, x => x.Total, cancellationToken);

        var resumos = await empresas
            .OrderBy(e => e.Nome)
            .Select(e => new
            {
                e.Id,
                e.Nome,
                e.Identificador,
                e.Ativo,
                e.Cidade,
                e.Estado,
                e.CorPrimaria,
                TemLogo = e.Logo != null,
            })
            .ToListAsync(cancellationToken);

        return resumos
            .Select(e => new EmpresaResumo(
                e.Id,
                e.Nome,
                e.Identificador,
                e.Ativo,
                e.Cidade,
                e.Estado,
                totalPorEmpresa.GetValueOrDefault(e.Id),
                e.CorPrimaria,
                e.TemLogo))
            .ToList();
    }

    /// <summary>Escapa caracteres curinga de LIKE (%) e underline (_) para busca literal.</summary>
    private static string EscapeLike(string valor) =>
        valor.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}