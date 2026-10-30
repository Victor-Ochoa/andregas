using Microsoft.EntityFrameworkCore;

using RevendaApp.Domain.Entities;
using RevendaApp.Infrastructure;

namespace RevendaApp.Web.Common;

/// <summary>
/// Recalcula o resumo de recorrência de compra (Cliente×Produto) a partir das vendas vigentes.
/// Chamado sempre que o conjunto de vendas de um par muda (nova venda, edição, exclusão). O
/// intervalo médio é a média dos gaps entre vendas consecutivas (gaps &lt; 7 dias são ignorados);
/// com uma única venda usa <see cref="Produto.DiasPadraoRecompra"/>; sem vendas remove a linha.
/// </summary>
public static class RecorrenciaCompraAprendizado
{
    /// <summary>Gap mínimo (em dias) entre compras para ser considerado um novo ciclo de consumo.</summary>
    public const int GapMinimoDias = 7;

    public static async Task RecalcularAsync(
        AppDbContext db, Guid clienteId, Guid produtoId, CancellationToken cancellationToken)
    {
        var datasVendas = await db.Vendas
            .Where(v => v.ClienteId == clienteId
                && v.ExcluidaEm == null
                && v.Itens.Any(i => i.ProdutoId == produtoId))
            .OrderBy(v => v.DataHora)
            .Select(v => v.DataHora)
            .ToListAsync(cancellationToken);

        var recorr = await db.RecorrenciasCompra
            .FirstOrDefaultAsync(r => r.ClienteId == clienteId && r.ProdutoId == produtoId, cancellationToken);

        if (datasVendas.Count == 0)
        {
            if (recorr is not null)
            {
                db.RecorrenciasCompra.Remove(recorr);
            }

            return;
        }

        var defaultDias = (await db.Produtos.FindAsync([produtoId], cancellationToken))?.DiasPadraoRecompra ?? 0;

        decimal intervalo = defaultDias;
        if (datasVendas.Count >= 2)
        {
            var gapsValidos = new List<double>();
            for (var i = 1; i < datasVendas.Count; i++)
            {
                var gap = (datasVendas[i] - datasVendas[i - 1]).TotalDays;
                if (gap >= GapMinimoDias)
                {
                    gapsValidos.Add(gap);
                }
            }

            // Só usa a média dos gaps se houver ao menos um gap válido; senão volta ao default.
            if (gapsValidos.Count > 0)
            {
                intervalo = (decimal)gapsValidos.Average();
            }
        }

        // A quantidade é o número de vendas vigentes (não dos gaps).
        var ultimaCompra = datasVendas[^1];
        if (recorr is null)
        {
            db.RecorrenciasCompra.Add(new RecorrenciaCompra(clienteId, produtoId, ultimaCompra, intervalo, datasVendas.Count));
        }
        else
        {
            recorr.Atualizar(ultimaCompra, intervalo, datasVendas.Count);
        }
    }
}