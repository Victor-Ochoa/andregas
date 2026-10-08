using AndreGas.Infrastructure;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Vendas.NovaVenda;

public sealed class BuscarClientePorTermoQueryHandler(AppDbContext db) : IQueryHandler<BuscarClientePorTermoQuery, IReadOnlyList<ClienteSugestao>>
{
    public async ValueTask<IReadOnlyList<ClienteSugestao>> Handle(BuscarClientePorTermoQuery query, CancellationToken cancellationToken)
    {
        var termo = query.Termo.Trim();
        if (termo.Length == 0)
        {
            return [];
        }

        // Busca parcial e case-insensitive por nome, telefone ou endereço usando ILIKE do
        // Postgres (EF.Functions.ILike). O caractere '%' (curinga de LIKE) é escapado para que a
        // busca seja literal. Ordena por nome e limita aos 10 primeiros para o autocomplete.
        var padrao = $"%{EscapeLike(termo)}%";
        var clientes = await db.Clientes
            .Where(c => EF.Functions.ILike(c.Nome, padrao)
                || EF.Functions.ILike(c.Telefone, padrao)
                || EF.Functions.ILike(c.Endereco, padrao))
            .OrderBy(c => c.Nome)
            .Take(10)
            .Select(c => new { c.Id, c.Nome, c.Telefone, c.Endereco, c.SaldoDevedor })
            .ToListAsync(cancellationToken);

        // Valor de entrega da venda mais recente de cada cliente (0 se ainda não houve venda ou se a
        // última venda não teve entrega) — usado para sugerir o campo na tela de nova venda.
        var resultado = new List<ClienteSugestao>(clientes.Count);
        foreach (var c in clientes)
        {
            var ultimaValorEntrega = await db.Vendas
                .Where(v => v.ClienteId == c.Id)
                .OrderByDescending(v => v.DataHora)
                .Select(v => (decimal?)v.ValorEntrega)
                .FirstOrDefaultAsync(cancellationToken) ?? 0m;

            resultado.Add(new ClienteSugestao(c.Id, c.Nome, c.Telefone, c.Endereco, c.SaldoDevedor, ultimaValorEntrega));
        }

        return resultado;
    }

    /// <summary>Escapa caracteres curinga de LIKE (%) e underline (_) para busca literal.</summary>
    private static string EscapeLike(string valor) =>
        valor.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}