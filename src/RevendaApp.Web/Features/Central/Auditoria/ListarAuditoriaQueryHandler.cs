using Mediator;

using Microsoft.EntityFrameworkCore;

using RevendaApp.Domain.Entities;
using RevendaApp.Infrastructure;
using RevendaApp.Infrastructure.Tenancy;
using RevendaApp.Web.Common;

namespace RevendaApp.Web.Features.Central.Auditoria;

/// <summary>
/// Lê a trilha de auditoria para a aba "Auditoria" da central, paginada e mais recente primeiro.
/// </summary>
/// <remarks>
/// <para>
/// <b>Filtro explícito por empresa (crítico):</b> <see cref="RegistroAuditoria"/> é a única entidade
/// de domínio <b>sem</b> filtro global — a linha "óbvia"
/// (<c>db.RegistrosAuditoria.OrderByDescending(...)</c>) devolveria as linhas de todas as empresas.
/// A visão da central (<c>EmpresaId == null</c>) é intencionalmente global, mas qualquer filtro por
/// empresa é aplicado no <c>Where</c>, nunca herdado do contexto.
/// </para>
/// <para>
/// <b>Autorização:</b> a política da rota (<c>ApenasSuperAdmin</c>) é só a primeira barreira; o
/// handler valida <c>tenant.EhSuperAdmin</c> <b>antes de qualquer query</b> — é o limite real.
/// </para>
/// <para>
/// <b>Sem N+1:</b> o nome da empresa vem de um <c>join</c> na mesma consulta; as linhas são
/// projetadas no banco (o base64 de logo, por exemplo, nunca sai da tabela <c>Empresas</c>).
/// </para>
/// </remarks>
public sealed class ListarAuditoriaQueryHandler(AppDbContext db, ITenantContext tenant)
    : IQueryHandler<ListarAuditoriaQuery, AuditoriaPaginada>
{
    public async ValueTask<AuditoriaPaginada> Handle(ListarAuditoriaQuery query, CancellationToken cancellationToken)
    {
        // Primeira instrução: é a visão da central (todas as empresas).
        if (!tenant.EhSuperAdmin)
        {
            throw new InvalidOperationException("Somente o super administrador da central pode consultar a auditoria.");
        }

        var registros = db.RegistrosAuditoria.AsNoTracking().AsQueryable();

        // Filtro explícito: a entidade não tem filtro global por empresa.
        if (query.EmpresaId is { } empresaId)
        {
            registros = registros.Where(r => r.EmpresaId == empresaId);
        }

        if (!string.IsNullOrWhiteSpace(query.Acao))
        {
            var acao = query.Acao.Trim();
            registros = registros.Where(r => r.Acao == acao);
        }

        // O período chega como data de Brasília (o que a tela mostra). "Ate" inclui o dia inteiro:
        // o limite superior vira a meia-noite local do dia seguinte, exclusivo.
        if (query.De is { } de)
        {
            var deUtc = BrasilTimeZone.ParaUtc(de.Date);
            registros = registros.Where(r => r.DataHora >= deUtc);
        }

        if (query.Ate is { } ate)
        {
            var ateUtcExclusivo = BrasilTimeZone.ParaUtc(ate.Date.AddDays(1));
            registros = registros.Where(r => r.DataHora < ateUtcExclusivo);
        }

        var pagina = Math.Max(1, query.Pagina);
        var tamanho = Math.Clamp(query.TamanhoPagina, 1, 100);

        // Contagem separada (a página pode vir vazia com total > 0 — ex.: página fora do intervalo).
        var total = await registros.CountAsync(cancellationToken);

        var itens = await (
                from r in registros
                join e in db.Set<Empresa>().AsNoTracking() on r.EmpresaId equals (Guid?)e.Id into empresas
                from e in empresas.DefaultIfEmpty()
                orderby r.DataHora descending, r.Id
                select new AuditoriaItem(
                    r.Id,
                    r.EmpresaId,
                    e != null ? e.Nome : null,
                    r.UsuarioId,
                    r.UsuarioNome,
                    r.Acao,
                    r.Detalhes,
                    r.DataHora,
                    r.Origem))
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .ToListAsync(cancellationToken);

        return new AuditoriaPaginada(itens, total, tamanho);
    }
}