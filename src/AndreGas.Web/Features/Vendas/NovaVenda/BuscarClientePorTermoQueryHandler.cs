using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Tenancy;
using AndreGas.Web.Common;

using Mediator;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Vendas.NovaVenda;

public sealed class BuscarClientePorTermoQueryHandler(AppDbContext db, ITenantContext tenant) : IQueryHandler<BuscarClientePorTermoQuery, IReadOnlyList<ClienteSugestao>>
{
    public async ValueTask<IReadOnlyList<ClienteSugestao>> Handle(BuscarClientePorTermoQuery query, CancellationToken cancellationToken)
    {
        // Exige empresa selecionada (autocomplete só busca clientes da empresa).
        _ = tenant.EmpresaIdRequerida("buscar cliente");

        var termo = query.Termo.Trim();
        if (termo.Length == 0)
        {
            return [];
        }

        // Busca parcial e case-insensitive por nome, telefone ou endereço usando ILIKE do
        // Postgres (EF.Functions.ILike). O backslash "\" é passado como escapeCharacter para
        // que os curingas de LIKE escapados por EscapeLike (%, _) sejam tratados como literal.
        // Sem o escape explícito, o Npgsql gera "ILIKE ... ESCAPE ''" e o "\" não escapa nada.
        // Ordena por nome e limita aos 10 primeiros para o autocomplete.
        var padrao = $"%{EscapeLike(termo)}%";
        var clientes = await db.Clientes
            .Where(c => EF.Functions.ILike(c.Nome, padrao, "\\")
                || EF.Functions.ILike(c.Telefone, padrao, "\\")
                || EF.Functions.ILike(c.Endereco, padrao, "\\"))
            .OrderBy(c => c.Nome)
            .Take(10)
            .Select(c => new { c.Id, c.Nome, c.Telefone, c.Endereco, c.SaldoDevedor })
            .ToListAsync(cancellationToken);

        // Valor de entrega da venda mais recente de cada cliente (0 se ainda não houve venda ou se a
        // última venda não teve entrega) — usado para sugerir o campo na tela de nova venda. Uma
        // única query agrupada evita o N+1 do loop anterior (1 query por cliente no autocomplete).
        var ultimasEntregas = await VendaQueries.ObtemUltimaEntregaPorClienteAsync(
            db, clientes.Select(c => c.Id), cancellationToken);

        return clientes
            .Select(c => new ClienteSugestao(
                c.Id, c.Nome, c.Telefone, c.Endereco, c.SaldoDevedor,
                ultimasEntregas.GetValueOrDefault(c.Id)))
            .ToList();
    }

    /// <summary>Escapa caracteres curinga de LIKE (%) e underline (_) para busca literal.</summary>
    private static string EscapeLike(string valor) =>
        valor.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}