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

            // Reverte o efeito de vasilhame que a venda causou no item:
            // - Empréstimo (produto que controla vasilhame): o vasilhame emprestado deixa de estar
            //   com o cliente (Emprestados − qtd) e as linhas abertas de EmprestimoVasilhame do
            //   empréstimo são desfeitas (FIFO).
            // - Troca (produto que controla vasilhame): o vazio que voltou ao depósito na venda
            //   sai de novo (Vasilhames − qtd).
            // Empréstimo "defensivo" em produto sem controle é tratado pelo RegistrarAsync como
            // troca (só Estoque −, sem vasilhame) → aqui também não há vasilhame a reverter.
            if (item.EhEmprestimo)
            {
                if (produto.ControlaVasilhame)
                {
                    produto.RegistrarDevolucaoVasilhame(item.Quantidade);
                    await DesfazerEmprestimosAbertosAsync(
                        db, venda.ClienteId, item.ProdutoId, item.Quantidade, venda.DataHora, cancellationToken);
                }
            }
            else if (produto.ControlaVasilhame)
            {
                produto.RegistrarSaidaVasilhame(item.Quantidade);
            }
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

    /// <summary>
    /// Desfaz <paramref name="quantidade"/> de vasilhames emprestados em aberto do cliente para o
    /// produto. Primeiro desfaz as linhas abertas do próprio período/Data da venda (a venda cria as
    /// linhas com <c>Data == DataHora</c> da venda — são as que devem sair ao reverter); se ainda
    /// faltar quantidade, consome as linhas abertas mais antigas (FIFO por <c>Data</c>, mesmo critério
    /// da devolução manual/Task 8), abatendo sempre o <see cref="EmprestimoVasilhame.Saldo"/> (para
    /// linha com devolução parcial, o já-devolvido não é recontado). Linhas removidas preservam a
    /// trilha de auditoria nos <see cref="HistoricoEstoque"/> de devolução.
    /// </summary>
    private static async Task DesfazerEmprestimosAbertosAsync(
        AppDbContext db, Guid clienteId, Guid produtoId, int quantidade, DateTime dataVenda, CancellationToken cancellationToken)
    {
        var restante = quantidade;

        // 1) Linhas abertas criadas próximo da venda (mesmo DataHora): a venda as cria com a data da
        //    venda, então removê-las primeiro desfaz exatamente o empréstimo da venda.
        var daVenda = await db.EmprestimosVasilhames
            .Where(e => e.ClienteId == clienteId
                && e.ProdutoId == produtoId
                && e.DevolvidoEm == null
                && e.Data >= dataVenda)
            .OrderBy(e => e.Data)
            .ToListAsync(cancellationToken);

        foreach (var emprestimo in daVenda)
        {
            if (restante <= 0)
            {
                break;
            }

            db.EmprestimosVasilhames.Remove(emprestimo);
            restante -= emprestimo.Saldo;
        }

        if (restante <= 0)
        {
            return;
        }

        // 2) Fallback FIFO pelas mais antigas (pré-venda), se a quantidade a reverter for maior que
        //    as linhas do período da venda (ex.: edição aumenta o empréstimo de um item).
        var anteriores = await db.EmprestimosVasilhames
            .Where(e => e.ClienteId == clienteId
                && e.ProdutoId == produtoId
                && e.DevolvidoEm == null
                && e.Data < dataVenda)
            .OrderBy(e => e.Data)
            .ToListAsync(cancellationToken);

        foreach (var emprestimo in anteriores)
        {
            if (restante <= 0)
            {
                break;
            }

            db.EmprestimosVasilhames.Remove(emprestimo);
            restante -= emprestimo.Saldo;
        }
    }
}