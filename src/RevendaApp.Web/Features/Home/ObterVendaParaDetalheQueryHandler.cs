using Mediator;

using Microsoft.EntityFrameworkCore;

using RevendaApp.Infrastructure;
using RevendaApp.Infrastructure.Tenancy;
using RevendaApp.Web.Common;

namespace RevendaApp.Web.Features.Home;

public sealed class ObterVendaParaDetalheQueryHandler(AppDbContext db, ITenantContext tenant)
    : IQueryHandler<ObterVendaParaDetalheQuery, VendaParaDetalheResult?>
{
    public async ValueTask<VendaParaDetalheResult?> Handle(ObterVendaParaDetalheQuery query, CancellationToken cancellationToken)
    {
        // Exige empresa selecionada (venda localizada pelo filtro global do tenant).
        _ = tenant.EmpresaIdRequerida("obter detalhe da venda");

        var venda = await db.Vendas
            .AsNoTracking()
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
            .Select(i => new VendaItemParaDetalhe(
                i.ProdutoId,
                i.Produto?.Nome ?? "Produto removido",
                i.Quantidade,
                i.PrecoUnitario,
                i.PrecoCustoUnitario,
                i.EhEmprestimo))
            .ToList();

        return new VendaParaDetalheResult(
            venda.Id,
            venda.ClienteId,
            venda.Cliente?.Nome ?? string.Empty,
            venda.Cliente?.Telefone ?? string.Empty,
            venda.FormaPagamento,
            venda.Status,
            venda.ValorTotal,
            venda.LucroTotal,
            venda.Desconto,
            venda.ValorEntrega,
            BrasilTimeZone.ParaBrasilia(venda.DataHora),
            itens);
    }
}