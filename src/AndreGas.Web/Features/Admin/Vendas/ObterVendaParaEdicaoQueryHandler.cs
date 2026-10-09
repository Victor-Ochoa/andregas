using AndreGas.Infrastructure;

using Mediator;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Admin.Vendas;

public sealed class ObterVendaParaEdicaoQueryHandler(AppDbContext db)
    : IQueryHandler<ObterVendaParaEdicaoQuery, ObterVendaParaEdicaoResult?>
{
    public async ValueTask<ObterVendaParaEdicaoResult?> Handle(ObterVendaParaEdicaoQuery query, CancellationToken cancellationToken)
    {
        var venda = await db.Vendas
            .Include(v => v.Itens)
                .ThenInclude(i => i.Produto)
            .Include(v => v.Cliente)
            .FirstOrDefaultAsync(v => v.Id == query.VendaId
                                      && v.ExcluidaEm == null, cancellationToken);

        if (venda is null)
        {
            return null;
        }

        var itens = venda.Itens
            .Select(i => new VendaItemEdicao(
                i.ProdutoId,
                i.Produto?.Nome ?? "Produto removido",
                i.Quantidade,
                i.PrecoUnitario))
            .ToList();

        return new ObterVendaParaEdicaoResult(
            venda.Id,
            venda.ClienteId,
            venda.Cliente?.Nome ?? string.Empty,
            venda.Cliente?.Telefone,
            venda.FormaPagamento,
            venda.Status,
            venda.Desconto,
            venda.ValorEntrega,
            venda.Cliente?.SaldoDevedor ?? 0m,
            itens);
    }
}