using AndreGas.Infrastructure;

using Mediator;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Clientes.Listar;

public sealed class ListarClientesPaginadoQueryHandler(AppDbContext db)
    : IQueryHandler<ListarClientesPaginadoQuery, ClienteListaPaginada>
{
    public async ValueTask<ClienteListaPaginada> Handle(ListarClientesPaginadoQuery query, CancellationToken cancellationToken)
    {
        // Filtros condicionais: cada um só entra no WHERE se foi informado, mantendo a query
        // executável também no provider InMemory dos testes unitários (que não traduz ILike).
        var baseQuery = db.Clientes.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Termo))
        {
            // Busca parcial e case-insensitive por nome, telefone ou endereço usando ILIKE do
            // Postgres (EF.Functions.ILike). O backslash "\" é passado como escapeCharacter para
            // que os curingas de LIKE escapados por EscapeLike (%, _) sejam tratados como literal.
            // Sem o escape explícito, o Npgsql gera "ILIKE ... ESCAPE ''" e o "\" não escapa nada.
            var padrao = $"%{EscapeLike(query.Termo.Trim())}%";
            baseQuery = baseQuery.Where(c => EF.Functions.ILike(c.Nome, padrao, "\\")
                || EF.Functions.ILike(c.Telefone, padrao, "\\")
                || EF.Functions.ILike(c.Endereco, padrao, "\\"));
        }

        if (query.Ativo is not null)
        {
            baseQuery = baseQuery.Where(c => c.Ativo == query.Ativo);
        }

        if (query.ComSaldoDevedor == true)
        {
            baseQuery = baseQuery.Where(c => c.SaldoDevedor > 0);
        }

        if (query.ComEmprestimo == true)
        {
            baseQuery = baseQuery.Where(c => db.EmprestimosVasilhames
                .Where(e => e.ClienteId == c.Id && e.DevolvidoEm == null)
                .Sum(e => e.Quantidade - e.QuantidadeDevolvida) > 0);
        }

        var pagina = Math.Max(1, query.Pagina);
        var tamanho = Math.Clamp(query.TamanhoPagina, 1, 100);

        var total = await baseQuery.CountAsync(cancellationToken);
        var totalPaginas = total == 0 ? 0 : (int)Math.Ceiling(total / (double)tamanho);

        var itens = await baseQuery
            .OrderBy(c => c.Nome)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .Select(c => new ClienteListItem(
                c.Id,
                c.Nome,
                c.Telefone,
                c.Endereco,
                c.SaldoDevedor,
                c.Ativo,
                db.EmprestimosVasilhames
                    .Where(e => e.ClienteId == c.Id && e.DevolvidoEm == null)
                    .Sum(e => e.Quantidade - e.QuantidadeDevolvida)))
            .ToListAsync(cancellationToken);

        return new ClienteListaPaginada(itens, total, totalPaginas);
    }

    /// <summary>Escapa caracteres curinga de LIKE (%) e underline (_) para busca literal.</summary>
    private static string EscapeLike(string valor) =>
        valor.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}