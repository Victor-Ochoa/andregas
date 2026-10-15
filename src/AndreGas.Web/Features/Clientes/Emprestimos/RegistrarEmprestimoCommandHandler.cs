using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.Web.Common;
using AndreGas.Web.Common.Authorization;

using Mediator;

using Microsoft.AspNetCore.Components.Authorization;

namespace AndreGas.Web.Features.Clientes.Emprestimos;

public sealed class RegistrarEmprestimoCommandHandler(AppDbContext db, AuthenticationStateProvider? authStateProvider = null,
    IUsuarioAutenticado? usuarioAutenticado = null)
    : ICommandHandler<RegistrarEmprestimoCommand, Guid>
{
    public async ValueTask<Guid> Handle(RegistrarEmprestimoCommand command, CancellationToken cancellationToken)
    {
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

        // O empréstimo manual pega um VASILHAME VÁZIO do depósito (disponível) e o entrega ao
        // cliente (emprestado). Exige vazio físico disponível: a saída de vazios lança se
        // insuficiente — um guard antes de criar qualquer linha/histórico (sem estado parcial).
        if (produto.QuantidadeVasilhames < command.Quantidade)
        {
            throw new InvalidOperationException(
                $"Vasilhames disponíveis insuficientes para emprestar {command.Quantidade}: há apenas {produto.QuantidadeVasilhames}.");
        }

        var emprestimo = new EmprestimoVasilhame(
            command.ClienteId,
            command.ProdutoId,
            command.Quantidade,
            data: DateTime.UtcNow,
            observacao: command.Observacao);
        db.EmprestimosVasilhames.Add(emprestimo);

        // Sai do disponível (depósito) e vai para o emprestado (com o cliente).
        produto.RegistrarSaidaVasilhame(command.Quantidade);
        produto.RegistrarEmprestimoVasilhame(command.Quantidade);

        var (usuario, _) = await UsuarioAtual.ObterAsync(authStateProvider, db, cancellationToken);
        db.HistoricosEstoque.Add(new HistoricoEstoque(
            produto.Id,
            TipoHistoricoEstoque.EmprestimoVasilhame,
            command.Quantidade == 1
                ? "Empréstimo de 1 vasilhame ao cliente"
                : $"Empréstimo de {command.Quantidade} vasilhames ao cliente",
            command.Observacao ?? "Empréstimo manual",
            usuario,
            quantidade: command.Quantidade));

        await db.SaveChangesAsync(cancellationToken);

        return emprestimo.Id;
    }
}