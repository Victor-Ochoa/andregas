using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.Web.Common;
using Mediator;
using Microsoft.AspNetCore.Components.Authorization;

namespace AndreGas.Web.Features.Estoque.Movimentar;

public sealed class RegistrarMovimentacaoEstoqueCommandHandler(AppDbContext db, AuthenticationStateProvider? authStateProvider = null)
    : ICommandHandler<RegistrarMovimentacaoEstoqueCommand, bool>
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

        var (usuario, _) = await UsuarioAtual.ObterAsync(authStateProvider, db, cancellationToken);
        var tipoHistorico = command.Tipo switch
        {
            TipoMovimentacaoEstoque.Entrada => TipoHistoricoEstoque.Entrada,
            TipoMovimentacaoEstoque.Saida => TipoHistoricoEstoque.Saida,
            _ => TipoHistoricoEstoque.Ajuste,
        };
        var descricao = command.Tipo switch
        {
            TipoMovimentacaoEstoque.Entrada => $"Entrada de {command.Quantidade} produto{(command.Quantidade == 1 ? string.Empty : "s")}",
            TipoMovimentacaoEstoque.Saida => $"Saída de {command.Quantidade} produto{(command.Quantidade == 1 ? string.Empty : "s")}",
            _ => $"Ajuste de estoque para {command.Quantidade}",
        };

        db.HistoricosEstoque.Add(new HistoricoEstoque(
            command.ProdutoId,
            tipoHistorico,
            descricao,
            command.Motivo,
            usuario,
            quantidade: command.Quantidade));

        await db.SaveChangesAsync(cancellationToken);

        return true;
    }
}
