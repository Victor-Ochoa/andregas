using AndreGas.Infrastructure;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Estoque.Editar;

public sealed class AtualizarProdutoCommandHandler(AppDbContext db) : ICommandHandler<AtualizarProdutoCommand, bool>
{
    public async ValueTask<bool> Handle(AtualizarProdutoCommand command, CancellationToken cancellationToken)
    {
        var produto = await db.Produtos.FindAsync([command.ProdutoId], cancellationToken);
        if (produto is null)
        {
            return false;
        }

        produto.AtualizarDados(command.Nome, command.Tipo, command.PrecoVenda, command.PrecoCusto, command.PrecoGasDoPovo, command.EstoqueMinimo);

        if (command.Ativo)
        {
            produto.Ativar();
        }
        else
        {
            produto.Desativar();
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}