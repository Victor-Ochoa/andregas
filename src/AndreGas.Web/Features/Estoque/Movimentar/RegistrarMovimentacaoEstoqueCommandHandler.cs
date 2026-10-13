using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.Web.Common;
using AndreGas.Web.Common.Authorization;

using Mediator;

using Microsoft.AspNetCore.Components.Authorization;

namespace AndreGas.Web.Features.Estoque.Movimentar;

public sealed class RegistrarMovimentacaoEstoqueCommandHandler(AppDbContext db, AuthenticationStateProvider? authStateProvider = null,
    IUsuarioAutenticado? usuarioAutenticado = null)
    : ICommandHandler<RegistrarMovimentacaoEstoqueCommand, bool>
{
    public async ValueTask<bool> Handle(RegistrarMovimentacaoEstoqueCommand command, CancellationToken cancellationToken)
    {
        // Operação restrita a administradores (ver explicação em CadastrarProdutoCommandHandler).
        if (usuarioAutenticado is not null)
        {
            await usuarioAutenticado.RequerAdminAsync(cancellationToken);
        }

        var produto = await db.Produtos.FindAsync([command.ProdutoId], cancellationToken);
        if (produto is null)
        {
            return false;
        }

        // Guarda do prompt de vasilhames: com a flag marcada, a quantidade adicional de vazios
        // precisa ser informada. Validada antes de qualquer mutação para não deixar estado parcial.
        // Produto sem controle de vasilhame ignora a flag por completo (comportamento atual).
        if (produto.ControlaVasilhame && command.AdicionarVasilhames &&
            command.Tipo == TipoMovimentacaoEstoque.Entrada && command.QuantidadeVasilhamesAdicionar <= 0)
        {
            throw new InvalidOperationException("Para adicionar vasilhames na entrada, informe uma quantidade maior que zero.");
        }

        int vasilhamesAdicionados = 0;
        switch (command.Tipo)
        {
            case TipoMovimentacaoEstoque.Entrada:
                produto.RegistrarEntrada(command.Quantidade);

                if (produto.ControlaVasilhame)
                {
                    // Cada cheio comprado traz um vazio como veículo de volta ao depósito.
                    produto.RegistrarEntradaVasilhame(command.Quantidade);
                    vasilhamesAdicionados = command.Quantidade;

                    if (command.AdicionarVasilhames)
                    {
                        // Prompt da tela: vazios que excedem a quantidade de cheios recebidos.
                        produto.RegistrarEntradaVasilhame(command.QuantidadeVasilhamesAdicionar);
                        vasilhamesAdicionados += command.QuantidadeVasilhamesAdicionar;
                    }
                }

                break;
            case TipoMovimentacaoEstoque.Saida:
                produto.RegistrarSaida(command.Quantidade);

                if (produto.ControlaVasilhame)
                {
                    // O vazio do produto vendido volta ao estoque de vasilhames disponíveis.
                    produto.RegistrarEntradaVasilhame(command.Quantidade);
                    vasilhamesAdicionados = command.Quantidade;
                }

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

        // Produto que controla vasilhame: um registro adicional de histórico descreve o movimento
        // dos vazios (entrada traz veículos vazios; saída devolve o vazio ao depósito).
        if (vasilhamesAdicionados > 0)
        {
            var descricaoVasilhame = command.Tipo switch
            {
                TipoMovimentacaoEstoque.Entrada => $"Entrada de {vasilhamesAdicionados} vasilhame{(vasilhamesAdicionados == 1 ? string.Empty : "s")}",
                _ => $"Saída de {vasilhamesAdicionados} vasilhame{(vasilhamesAdicionados == 1 ? string.Empty : "s")} — vazios devolvidos ao depósito",
            };

            db.HistoricosEstoque.Add(new HistoricoEstoque(
                command.ProdutoId,
                TipoHistoricoEstoque.Vasilhame,
                descricaoVasilhame,
                command.Motivo,
                usuario,
                quantidade: vasilhamesAdicionados));
        }

        await db.SaveChangesAsync(cancellationToken);

        return true;
    }
}