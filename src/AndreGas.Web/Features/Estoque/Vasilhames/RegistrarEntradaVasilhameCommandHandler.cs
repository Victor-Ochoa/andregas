using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.Web.Common;
using AndreGas.Web.Common.Authorization;

using Mediator;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Estoque.Vasilhames;

/// <summary>Registra uma entrada manual de vasilhames vazios no depósito de um produto.</summary>
public sealed class RegistrarEntradaVasilhameCommandHandler(AppDbContext db, AuthenticationStateProvider? authStateProvider = null,
    IUsuarioAutenticado? usuarioAutenticado = null)
    : ICommandHandler<RegistrarEntradaVasilhameCommand, bool>
{
    public async ValueTask<bool> Handle(RegistrarEntradaVasilhameCommand command, CancellationToken cancellationToken)
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

        produto.RegistrarEntradaVasilhame(command.Quantidade);

        var (usuario, _) = await UsuarioAtual.ObterAsync(authStateProvider, db, cancellationToken);
        db.HistoricosEstoque.Add(new HistoricoEstoque(
            produto.Id,
            TipoHistoricoEstoque.Vasilhame,
            command.Quantidade == 1
                ? "Entrada de 1 vasilhame vazio ao depósito"
                : $"Entrada de {command.Quantidade} vasilhames vazios ao depósito",
            command.Observacao ?? "Entrada manual de vazios",
            usuario,
            quantidade: command.Quantidade));

        await db.SaveChangesAsync(cancellationToken);

        return true;
    }
}