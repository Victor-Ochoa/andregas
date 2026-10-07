using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.Web.Common;
using Mediator;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Vendas.NovaVenda;

public sealed class RegistrarVendaCommandHandler(AppDbContext db, AuthenticationStateProvider? authStateProvider = null)
    : ICommandHandler<RegistrarVendaCommand, RegistrarVendaResult>
{
    public async ValueTask<RegistrarVendaResult> Handle(RegistrarVendaCommand command, CancellationToken cancellationToken)
    {
        // Usuário autenticado que registra a venda (gaveta apenas de registro); "sistema" quando não há.
        var (usuario, vendedorId) = await UsuarioAtual.ObterAsync(authStateProvider, db, cancellationToken);

        var telefoneNormalizado = Cliente.NormalizarTelefone(command.Telefone);
        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Telefone == telefoneNormalizado, cancellationToken);

        if (cliente is null)
        {
            cliente = new Cliente(command.NomeClienteNovo!, command.Telefone, command.EnderecoClienteNovo!);
            db.Clientes.Add(cliente);
        }

        var venda = new Venda(cliente.Id, command.FormaPagamento);
        venda.DefinirVendedor(vendedorId);

        foreach (var itemInput in command.Itens)
        {
            var produto = await db.Produtos.FindAsync([itemInput.ProdutoId], cancellationToken)
                ?? throw new InvalidOperationException($"Produto '{itemInput.ProdutoId}' não encontrado.");

            if (!produto.Ativo)
            {
                throw new InvalidOperationException($"O produto '{produto.Nome}' está desabilitado e não pode ser vendido.");
            }

            var precoUnitario = produto.PrecoParaFormaPagamento(command.FormaPagamento);
            venda.AdicionarItem(produto.Id, itemInput.Quantidade, precoUnitario, produto.PrecoCusto);

            produto.RegistrarSaida(itemInput.Quantidade);
            db.MovimentacoesEstoque.Add(new MovimentacaoEstoque(produto.Id, TipoMovimentacaoEstoque.Saida, itemInput.Quantidade, "Venda"));
        }

        if (command.Desconto > 0)
        {
            venda.AplicarDesconto(command.Desconto);
        }

        venda.DefinirValorEntrega(command.ValorEntrega);

        // Só soma ao saldo devedor quando a venda é fiado; demais formas de pagamento são
        // liquidadas no ato e não alteram o saldo do cliente.
        if (command.FormaPagamento == FormaPagamento.Fiado)
        {
            cliente.AdicionarSaldoDevedor(venda.ValorTotal);
        }
        else
        {
            // Venda paga no ato: registra o pagamento recebido (não abate saldo devedor, pois
            // a venda não foi fiado).
            db.Pagamentos.Add(new Pagamento(cliente.Id, venda.ValorTotal, command.FormaPagamento, "Venda"));
        }

        db.Vendas.Add(venda);
        await db.SaveChangesAsync(cancellationToken);

        // Histórico de estoque consolidado por produto da venda (soma as quantidades do mesmo
        // produto), evitando uma linha por item. A MovimentacaoEstoque acima mantém 1 linha/item.
        foreach (var grupo in command.Itens.GroupBy(i => i.ProdutoId))
        {
            var quantidade = grupo.Sum(i => i.Quantidade);
            var palavra = quantidade == 1 ? "produto" : "produtos";
            db.HistoricosEstoque.Add(new HistoricoEstoque(
                grupo.Key,
                TipoHistoricoEstoque.Venda,
                $"Venda de {quantidade} {palavra}",
                "Venda",
                usuario,
                quantidade: quantidade,
                vendaId: venda.Id));
        }

        await db.SaveChangesAsync(cancellationToken);

        return new RegistrarVendaResult(venda.Id, cliente.Id, venda.ValorTotal, venda.LucroTotal);
    }
}
