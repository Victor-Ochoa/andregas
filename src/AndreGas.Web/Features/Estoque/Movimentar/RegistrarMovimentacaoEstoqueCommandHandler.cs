using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using Mediator;

namespace AndreGas.Web.Features.Estoque.Movimentar;

public sealed class RegistrarMovimentacaoEstoqueCommandHandler(AppDbContext db) : ICommandHandler<RegistrarMovimentacaoEstoqueCommand, bool>
{
    public async ValueTask<bool> Handle(RegistrarMovimentacaoEstoqueCommand command, CancellationToken cancellationToken)
    {
        var produto = await db.Produtos.FindAsync([command.ProdutoId], cancellationToken);
        if (produto is null)
        {
            return false;
        }

        switch (command.Tipo)
        {
            case TipoMovimentacaoEstoque.Entrada:
                produto.RegistrarEntrada(command.Quantidade);
                break;
            case TipoMovimentacaoEstoque.Saida:
                produto.RegistrarSaida(command.Quantidade);
                break;
            case TipoMovimentacaoEstoque.Ajuste:
                // Para ajuste, a quantidade do comando representa o novo total em estoque.
                produto.AjustarEstoque(command.Quantidade);
                break;
        }

        var movimentacao = new MovimentacaoEstoque(command.ProdutoId, command.Tipo, command.Quantidade, command.Motivo);
        db.MovimentacoesEstoque.Add(movimentacao);

        await db.SaveChangesAsync(cancellationToken);

        return true;
    }
}
