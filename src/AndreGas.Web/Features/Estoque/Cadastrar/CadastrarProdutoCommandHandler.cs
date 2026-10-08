using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.Web.Common;
using AndreGas.Web.Common.Authorization;
using Mediator;
using Microsoft.AspNetCore.Components.Authorization;

namespace AndreGas.Web.Features.Estoque.Cadastrar;

public sealed class CadastrarProdutoCommandHandler(AppDbContext db, AuthenticationStateProvider? authStateProvider = null,
    IUsuarioAutenticado? usuarioAutenticado = null)
    : ICommandHandler<CadastrarProdutoCommand, Guid>
{
    public async ValueTask<Guid> Handle(CadastrarProdutoCommand command, CancellationToken cancellationToken)
    {
        // Operação restrita a administradores. Em produção o DI sempre resolve o serviço scoped;
        // null significa que quem chamou construiu o handler manualmente (testes) e não exigiu a guarda.
        if (usuarioAutenticado is not null)
        {
            await usuarioAutenticado.RequerAdminAsync(cancellationToken);
        }

        var produto = new Produto(
            command.Nome,
            command.Tipo,
            command.PrecoVenda,
            command.PrecoCusto,
            command.PrecoGasDoPovo,
            command.EstoqueMinimo);

        db.Produtos.Add(produto);
        await db.SaveChangesAsync(cancellationToken);

        var (usuario, _) = await UsuarioAtual.ObterAsync(authStateProvider, db, cancellationToken);
        db.HistoricosEstoque.Add(new HistoricoEstoque(
            produto.Id,
            TipoHistoricoEstoque.Cadastro,
            "Cadastro inicial",
            "Cadastro inicial",
            usuario));

        await db.SaveChangesAsync(cancellationToken);

        return produto.Id;
    }
}
