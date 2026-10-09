using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.Web.Common;

using Mediator;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Clientes.Pagar;

public sealed class RegistrarPagamentoCommandHandler(AppDbContext db, VendasAtualizadasNotifier notifier)
    : ICommandHandler<RegistrarPagamentoCommand, RegistrarPagamentoResult>
{
    public async ValueTask<RegistrarPagamentoResult> Handle(RegistrarPagamentoCommand command, CancellationToken cancellationToken)
    {
        var cliente = await db.Clientes.FindAsync([command.ClienteId], cancellationToken)
            ?? throw new InvalidOperationException($"Cliente '{command.ClienteId}' não encontrado.");

        var pagamento = new Pagamento(cliente.Id, command.Valor, command.FormaPagamento, command.Observacao);
        db.Pagamentos.Add(pagamento);

        cliente.RegistrarPagamento(command.Valor);

        // Quita as vendas fiado mais antigas primeiro (FIFO), até o valor pago cobrir o saldo.
        var vendasFiado = await db.Vendas
            .Where(v => v.ClienteId == cliente.Id
                        && v.ExcluidaEm == null
                        && v.FormaPagamento == FormaPagamento.Fiado
                        && v.Status == VendaStatus.FiadoAberto)
            .OrderBy(v => v.DataHora)
            .Include(v => v.Itens)
            .ToListAsync(cancellationToken);

        var valorPago = command.Valor;
        foreach (var venda in vendasFiado)
        {
            if (valorPago <= 0)
            {
                break;
            }

            if (valorPago >= venda.ValorTotal)
            {
                venda.MarcarFiadoQuitado();
                valorPago -= venda.ValorTotal;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await notifier.NotificarVendaRegistrada();

        return new RegistrarPagamentoResult(pagamento.Id, cliente.SaldoDevedor);
    }
}