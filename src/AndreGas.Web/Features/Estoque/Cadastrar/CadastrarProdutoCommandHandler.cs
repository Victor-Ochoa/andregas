using AndreGas.Domain.Entities;
using AndreGas.Infrastructure;
using Mediator;

namespace AndreGas.Web.Features.Estoque.Cadastrar;

public sealed class CadastrarProdutoCommandHandler(AppDbContext db) : ICommandHandler<CadastrarProdutoCommand, Guid>
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

        return produto.Id;
    }
}
