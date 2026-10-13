using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.Web.Common;
using AndreGas.Web.Common.Authorization;

using Mediator;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Clientes.Emprestimos;

public sealed class RegistrarDevolucaoVasilhameCommandHandler(AppDbContext db, AuthenticationStateProvider? authStateProvider = null,
    IUsuarioAutenticado? usuarioAutenticado = null)
    : ICommandHandler<RegistrarDevolucaoVasilhameCommand, bool>
{
    public async ValueTask<bool> Handle(RegistrarDevolucaoVasilhameCommand command, CancellationToken cancellationToken)
    {
        // Operação restrita a administradores (mesmo padrão dos handlers de mutação de estoque).
        if (usuarioAutenticado is not null)
        {
            await usuarioAutenticado.RequerAdminAsync(cancellationToken);
        }

        var produto = await db.Produtos.FindAsync([command.ProdutoId], cancellationToken)
            ?? throw new InvalidOperationException("Produto não encontrado.");

        // Guarda de integridade: o cliente só pode devolver o que ainda tem em aberto para esse
        // produto (soma dos saldos dos empréstimos não quitados). Validada antes de qualquer
        // mutação para não deixar estado parcial.
        var saldoEmAberto = await db.EmprestimosVasilhames
            .Where(e => e.ClienteId == command.ClienteId
                        && e.ProdutoId == command.ProdutoId
                        && e.DevolvidoEm == null)
            .SumAsync(e => (int?)(e.Quantidade - e.QuantidadeDevolvida), cancellationToken) ?? 0;

        if (saldoEmAberto < command.Quantidade)
        {
            throw new InvalidOperationException("Quantidade devolvida não pode exceder os vasilhames emprestados");
        }

        // Regra 1 — quita do saldo em aberto do cliente, sempre: reduz os emprestados do produto
        // e consome a devolução contra os empréstimos mais antigos (FIFO por Data).
        produto.RegistrarDevolucaoVasilhame(command.Quantidade);

        var emAbertoFifo = await db.EmprestimosVasilhames
            .Where(e => e.ClienteId == command.ClienteId
                        && e.ProdutoId == command.ProdutoId
                        && e.DevolvidoEm == null)
            .OrderBy(e => e.Data)
            .ToListAsync(cancellationToken);

        var restante = command.Quantidade;
        foreach (var emprestimo in emAbertoFifo)
        {
            if (restante <= 0)
            {
                break;
            }

            var aQuitar = Math.Min(restante, emprestimo.Saldo);
            emprestimo.RegistrarDevolucao(aQuitar);
            restante -= aQuitar;
        }

        // Regras 2 e 3 — reabastecer x manter: "devolve o veículo cheio para vender de novo".
        // Se há estoque de cheio suficiente, o vazio devolvido volta como produto cheio; senão o
        // estoque fica como está (o vazio não vira um cheio "do nada").
        var reabasteceu = produto.ControlaVasilhame && produto.QuantidadeEstoque >= command.Quantidade;
        if (reabasteceu)
        {
            produto.RegistrarEntrada(command.Quantidade);
        }

        var (usuario, _) = await UsuarioAtual.ObterAsync(authStateProvider, db, cancellationToken);
        var descricao = reabasteceu
            ? command.Quantidade == 1
                ? "Devolução de 1 vasilhame reabastecido no estoque"
                : $"Devolução de {command.Quantidade} vasilhames reabastecidos no estoque"
            : command.Quantidade == 1
                ? "Devolução de 1 vasilhame ao cliente (estoque mantido)"
                : $"Devolução de {command.Quantidade} vasilhames ao cliente (estoque mantido)";

        db.HistoricosEstoque.Add(new HistoricoEstoque(
            produto.Id,
            TipoHistoricoEstoque.DevolucaoVasilhame,
            descricao,
            command.Observacao ?? "Devolução manual",
            usuario,
            quantidade: command.Quantidade));

        await db.SaveChangesAsync(cancellationToken);

        return true;
    }
}