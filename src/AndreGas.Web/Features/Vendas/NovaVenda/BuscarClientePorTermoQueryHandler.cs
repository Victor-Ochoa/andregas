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

        // Busca parcial e case-insensitive por nome, telefone ou endereço, ordenada por nome,
        // limitada aos 10 primeiros para alimentar o autocomplete. O termo é normalizado em
        // minúsculas e comparado com os campos também em minúsculas, para funcionar tanto no
        // Postgres (LIKE) quanto no provider InMemory dos testes.
        var termoLower = termo.ToLowerInvariant();

        // O EF Core não consegue traduzir `string.Contains(termo, StringComparison)` para SQL e
        // o provider InMemory dos testes também não; a comparação case-insensitive é feita
        // normalizando ambos os lados com ToLowerInvariant (aceito pelo analisador com o pragma).
#pragma warning disable CA1862 // Prefira overload com StringComparison; indisponível em queries EF.
        var clientes = await db.Clientes
            .Where(c => c.Nome.ToLowerInvariant().Contains(termoLower)
                || c.Telefone.ToLowerInvariant().Contains(termoLower)
                || c.Endereco.ToLowerInvariant().Contains(termoLower))
#pragma warning restore CA1862
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
}