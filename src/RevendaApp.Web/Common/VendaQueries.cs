using Microsoft.EntityFrameworkCore;

using RevendaApp.Domain.Entities;
using RevendaApp.Infrastructure;

namespace RevendaApp.Web.Common;

/// <summary>
/// Consultas de leitura de vendas compartilhadas entre handlers. Antes os mesmos critérios (ex.:
/// "última entrega do cliente") estavam duplicados e, no autocomplete de cliente, executavam uma
/// query por resultado (N+1). Centralizar aqui garante critério único e um único hit ao banco.
/// </summary>
public static class VendaQueries
{
    /// <summary>
    /// Valor de entrega da venda mais recente NÃO excluída do cliente (0 se ainda não houve venda
    /// ou se a última venda não teve entrega). Usado para sugerir o campo na nova venda.
    /// <paramref name="clientesIds"/> vazio → retorna dicionário vazio (sem tocar no banco).
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, decimal>> ObtemUltimaEntregaPorClienteAsync(
        AppDbContext db,
        IEnumerable<Guid> clientesIds,
        CancellationToken cancellationToken)
    {
        var ids = clientesIds.ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, decimal>();
        }

        // Uma única query agrupada por cliente traz a última entrega de todos de uma vez;
        // antes era um loop com uma query por cliente (N+1) no autocomplete.
        return await db.Vendas
            .AsNoTracking()
            .Where(v => v.ExcluidaEm == null && ids.Contains(v.ClienteId))
            .OrderByDescending(v => v.DataHora)
            .GroupBy(v => v.ClienteId)
            .Select(g => new
            {
                ClienteId = g.Key,
                UltimaEntrega = g.Select(v => (decimal?)v.ValorEntrega).FirstOrDefault() ?? 0m,
            })
            .ToDictionaryAsync(x => x.ClienteId, x => x.UltimaEntrega, cancellationToken);
    }

    /// <summary>
    /// Busca a última entrega de um único cliente (0 se não houver venda/entrega). Reusa a versão
    /// agrupada sem duplicar o critério.
    /// </summary>
    public static async Task<decimal> ObtemUltimaEntregaAsync(
        AppDbContext db,
        Guid clienteId,
        CancellationToken cancellationToken)
    {
        var mapa = await ObtemUltimaEntregaPorClienteAsync(db, [clienteId], cancellationToken);
        return mapa.GetValueOrDefault(clienteId);
    }
}