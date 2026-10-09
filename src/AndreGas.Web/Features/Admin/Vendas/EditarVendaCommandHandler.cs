using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.Web.Common;
using AndreGas.Web.Common.Authorization;
using AndreGas.Web.Features.Vendas.NovaVenda;

using Mediator;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Admin.Vendas;

/// <summary>
/// Edita uma venda existente re-criando-a a partir do estado novo, numa única transação: reverte
/// os efeitos da venda original (saldo devedor de fiado + estoque + pagamento de venda paga no
/// ato), marca a antiga como excluída e registra a nova via
/// <see cref="RegistrarVendaCommandHandler.RegistrarAsync"/> — preservando preço personalizado,
/// regras de Gás do Povo, rateio de desconto e histórico.
/// </summary>
public sealed class EditarVendaCommandHandler(
    AppDbContext db,
    IUsuarioAutenticado? usuarioAutenticado = null,
    AuthenticationStateProvider? authStateProvider = null,
    VendasAtualizadasNotifier? notifier = null)
    : ICommandHandler<EditarVendaCommand, EditarVendaResult>
{
    public async ValueTask<EditarVendaResult> Handle(EditarVendaCommand command, CancellationToken cancellationToken)
    {
        // Restrito a administradores (reforço no servidor, como o estoque).
        if (usuarioAutenticado is not null)
        {
            await usuarioAutenticado.RequerAdminAsync(cancellationToken);
        }

        var venda = await db.Vendas
            .Include(v => v.Itens)
            .Include(v => v.Cliente)
            .FirstOrDefaultAsync(v => v.Id == command.VendaId, cancellationToken)
            ?? throw new InvalidOperationException("A venda a editar não foi encontrada.");

        if (venda.ExcluidaEm is not null)
        {
            throw new InvalidOperationException("Não é possível editar uma venda excluída.");
        }

        var cliente = venda.Cliente!;

        // Regra fiado: só é editável se o saldo devedor do cliente cobre a venda anterior
        // (ainda em aberto). Se quitada/saldo insuficiente → bloqueia.
        if (venda.FormaPagamento == FormaPagamento.Fiado
            && cliente.SaldoDevedor < venda.ValorTotal)
        {
            throw new InvalidOperationException(
                "Não é possível editar: o saldo devedor do cliente não cobre o valor desta venda fiado (já quitada).");
        }

        // Marca a venda antiga como excluída ANTES de re-criar, para que as queries (que filtram
        // por ExcluidaEm == null) não a considerem durante toda a transação.
        venda.Excluir();

        // Reverte o impacto da venda original (saldo devedor fiado + estoque + pagamento de venda paga).
        await ReversaoVendaHelper.ReverterAsync(db, venda, cancellationToken);

        // Usuário autenticado para a nova venda (gaveta de registro).
        var (usuario, vendedorId) = await UsuarioAtual.ObterAsync(authStateProvider, db, cancellationToken);

        // Re-cria a venda a partir do novo estado, no mesmo DbContext (transação única).
        var resultado = await RegistrarVendaCommandHandler.RegistrarAsync(db,
            new RegistrarVendaCommand(
                cliente.Id,
                null,
                null,
                null,
                command.FormaPagamento,
                command.Desconto,
                command.Itens,
                command.ValorEntrega),
            vendedorId,
            usuario,
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        if (notifier is not null)
        {
            await notifier.NotificarVendaRegistrada();
        }

        var clienteAtual = await db.Clientes.FindAsync([cliente.Id], cancellationToken);
        return new EditarVendaResult(
            resultado.VendaId,
            cliente.Id,
            resultado.ValorTotal,
            resultado.LucroTotal,
            clienteAtual?.SaldoDevedor ?? 0m);
    }
}