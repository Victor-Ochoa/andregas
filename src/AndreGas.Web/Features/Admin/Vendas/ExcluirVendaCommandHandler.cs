using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.Web.Common;
using AndreGas.Web.Common.Authorization;

using Mediator;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Admin.Vendas;

public sealed class ExcluirVendaCommandHandler(
    AppDbContext db,
    IUsuarioAutenticado? usuarioAutenticado = null,
    VendasAtualizadasNotifier? notifier = null)
    : ICommandHandler<ExcluirVendaCommand, ExcluirVendaResult>
{
    public async ValueTask<ExcluirVendaResult> Handle(ExcluirVendaCommand command, CancellationToken cancellationToken)
    {
        // Restrito a administradores (reforço no servidor).
        if (usuarioAutenticado is not null)
        {
            await usuarioAutenticado.RequerAdminAsync(cancellationToken);
        }

        var venda = await db.Vendas
            .Include(v => v.Itens)
            .Include(v => v.Cliente)
            .FirstOrDefaultAsync(v => v.Id == command.VendaId, cancellationToken)
            ?? throw new InvalidOperationException("A venda a excluir não foi encontrada.");

        if (venda.ExcluidaEm is not null)
        {
            throw new InvalidOperationException("A venda já foi excluída.");
        }

        var cliente = venda.Cliente!;

        // Regra fiado: bloquear se o saldo devedor não cobre a venda (já quitada). Se cobre
        // (saldo "negativo"/em aberto), o estorno pode ocorrer com segurança.
        if (venda.FormaPagamento == FormaPagamento.Fiado
            && cliente.SaldoDevedor < venda.ValorTotal)
        {
            throw new InvalidOperationException(
                "Não é possível excluir: o saldo devedor do cliente não cobre o valor desta venda fiado (já quitada).");
        }

        // Estorna saldo devedor (fiado) e estoque; remove pagamento de venda paga no ato.
        await ReversaoVendaHelper.ReverterAsync(db, venda, cancellationToken);

        // Exclusão lógica: a venda permanece no banco (não quebrar FKs) mas deixa de contar.
        venda.Excluir();

        await db.SaveChangesAsync(cancellationToken);

        if (notifier is not null)
        {
            await notifier.NotificarVendaRegistrada();
        }

        return new ExcluirVendaResult(venda.Id, Sucesso: true);
    }
}