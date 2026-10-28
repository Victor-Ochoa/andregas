using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Tenancy;
using AndreGas.Web.Common;
using AndreGas.Web.Common.Authorization;

using Mediator;

using Microsoft.AspNetCore.Components.Authorization;

namespace AndreGas.Web.Features.Estoque.Vasilhames;

/// <summary>
/// Registra uma movimentação de vasilhames <b>disponíveis</b> (vazios) de um produto que controla
/// vasilhame. A entrada soma aos vazios; a saída subtrai (validando que há disponíveis); o ajuste
/// define o total exato de vazios. Valoriza apenas <c>QuantidadeVasilhames</c> — nunca mexe nos
/// emprestados. Sempre grava histórico de estoque (<see cref="TipoHistoricoEstoque.Vasilhame"/>).
/// </summary>
public sealed class RegistrarMovimentacaoVasilhameCommandHandler(
    AppDbContext db,
    ITenantContext tenant,
    AuthenticationStateProvider? authStateProvider = null,
    IUsuarioAutenticado? usuarioAutenticado = null)
    : ICommandHandler<RegistrarMovimentacaoVasilhameCommand, bool>
{
    public async ValueTask<bool> Handle(RegistrarMovimentacaoVasilhameCommand command, CancellationToken cancellationToken)
    {
        // Exige empresa selecionada (o produto é localizado pelo filtro global do tenant).
        _ = tenant.EmpresaIdRequerida("movimentar vasilhame");

        // Operação restrita a administradores (mesmo padrão dos handlers de mutação de estoque).
        if (usuarioAutenticado is not null)
        {
            await usuarioAutenticado.RequerAdminAsync(cancellationToken);
        }

        var produto = await db.Produtos.FindAsync([command.ProdutoId], cancellationToken)
            ?? throw new InvalidOperationException("Produto não encontrado.");

        if (!produto.ControlaVasilhame)
        {
            throw new InvalidOperationException($"O produto '{produto.Nome}' não controla vasilhames.");
        }

        switch (command.Tipo)
        {
            case TipoMovimentacaoEstoque.Entrada:
                // Reposição manual de vazios no depósito (ex.: recolhidos na rota ou estoque inicial).
                produto.RegistrarEntradaVasilhame(command.Quantidade);
                break;
            case TipoMovimentacaoEstoque.Saida:
                // Saída manual de vazios (ex.: vazios entregues fora do fluxo de venda/compra).
                // Valida antes de chamar o domínio para lançar InvalidOperationException (o padrão
                // dos handlers de estoque), em vez do ArgumentOutOfRangeException do domínio, e
                // exibe a mensagem amigável na dialog.
                if (produto.QuantidadeVasilhames < command.Quantidade)
                {
                    throw new InvalidOperationException(
                        $"Vasilhames disponíveis insuficientes para a saída de {command.Quantidade} '{produto.Nome}': há apenas {produto.QuantidadeVasilhames}.");
                }

                produto.RegistrarSaidaVasilhame(command.Quantidade);
                break;
            case TipoMovimentacaoEstoque.Ajuste:
                // Define o total de vazios disponíveis (inventário/conferência).
                produto.AjustarVasilhames(command.Quantidade);
                break;
        }

        var (usuario, _) = await UsuarioAtual.ObterAsync(authStateProvider, db, cancellationToken);
        var descricao = command.Tipo switch
        {
            TipoMovimentacaoEstoque.Entrada => command.Quantidade == 1
                ? "Entrada de 1 vasilhame vazio ao depósito"
                : $"Entrada de {command.Quantidade} vasilhames vazios ao depósito",
            TipoMovimentacaoEstoque.Saida => command.Quantidade == 1
                ? "Saída de 1 vasilhame vazio do depósito"
                : $"Saída de {command.Quantidade} vasilhames vazios do depósito",
            _ => $"Ajuste de vasilhames para {command.Quantidade} disponíveis",
        };

        db.HistoricosEstoque.Add(new HistoricoEstoque(
            produto.Id,
            TipoHistoricoEstoque.Vasilhame,
            descricao,
            command.Observacao ?? "Movimentação manual de vazios",
            usuario,
            quantidade: command.Quantidade));

        await db.SaveChangesAsync(cancellationToken);

        return true;
    }
}