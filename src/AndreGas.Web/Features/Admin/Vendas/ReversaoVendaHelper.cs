using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Admin.Vendas;

/// <summary>
/// Reverte os efeitos de uma venda no estoque e no saldo devedor do cliente, sem apagá-la do
/// banco (a exclusão é lógica via <see cref="Venda.Excluir"/>). Usado pelos handlers de
/// exclusão e edição de venda.
/// </summary>
public static class ReversaoVendaHelper
{
    /// <summary>
    /// Desfaz o que a venda causou: devolve a quantidade vendida ao estoque de cada produto
    /// (revertendo a <see cref="MovimentacaoEstoque"/> original), remove os históricos de estoque
    /// vinculados à venda e, se for fiado, abate o valor total da venda do saldo devedor.
    /// Também remove o <see cref="Pagamento"/> gerado no ato quando a venda não é fiado
    /// (pagamento vinculado à venda), para não somar dinheiro de uma venda desfeita.
    /// </summary>
    public static async Task ReverterAsync(AppDbContext db, Venda venda, CancellationToken cancellationToken)
    {
        // Devolve o estoque vendido de cada produto (estorno). As movimentações de saída originais
        // NÃO são removidas (elas têm sem apenas o ProdutoId/Motivo, sem vendaId, e remover por
        // match apagaria saídas de outras vendas ativas). Em vez disso, registra uma movimentação
        // de entrada de reversão por item, mantendo a trilha de auditoria completa.
        var produtos = await db.Produtos
            .Where(p => venda.Itens.Select(i => i.ProdutoId).Contains(p.Id))
            .ToListAsync(cancellationToken);

        foreach (var item in venda.Itens)
        {
            var produto = produtos.FirstOrDefault(p => p.Id == item.ProdutoId);
            if (produto is null)
            {
                // Produto removido do catálogo: não há estoque a devolver nem FK para movimentar.
                continue;
            }

            produto.RegistrarEntrada(item.Quantidade);
            db.MovimentacoesEstoque.Add(new MovimentacaoEstoque(
                item.ProdutoId,
                TipoMovimentacaoEstoque.Entrada,
                item.Quantidade,
                "Estorno de venda excluída"));
        }

        // História de estoque vinculada à venda (visualização) também é revertida.
        db.HistoricosEstoque.RemoveRange(
            db.HistoricosEstoque.Where(h => h.VendaId == venda.Id));

        if (venda.FormaPagamento == FormaPagamento.Fiado)
        {
            var cliente = await db.Clientes.FindAsync([venda.ClienteId], cancellationToken);
            if (cliente is not null)
            {
                cliente.RegistrarPagamento(venda.ValorTotal);
            }
        }
        else
        {
            // Venda não-fiado gerou um Pagamento no ato (vínculo Pagamento.VendaId). Remove para
            // não contabilizar um recebimento de uma venda desfeita.
            db.Pagamentos.RemoveRange(
                db.Pagamentos.Where(p => p.VendaId == venda.Id));
        }
    }
}