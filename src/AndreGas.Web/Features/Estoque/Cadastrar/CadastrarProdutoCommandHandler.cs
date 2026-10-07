using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.Web.Common;
using Mediator;
using Microsoft.AspNetCore.Components.Authorization;

namespace AndreGas.Web.Features.Estoque.Cadastrar;

public sealed class CadastrarProdutoCommandHandler(AppDbContext db, AuthenticationStateProvider? authStateProvider = null)
    : ICommandHandler<CadastrarProdutoCommand, Guid>
{
    public async ValueTask<Guid> Handle(CadastrarProdutoCommand command, CancellationToken cancellationToken)
    {
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
