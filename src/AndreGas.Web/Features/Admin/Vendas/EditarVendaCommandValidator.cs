using AndreGas.Infrastructure;

using FluentValidation;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Admin.Vendas;

/// <summary>Valida o comando de edição de venda: existência da venda, itens não vazios,
/// quantidades positivas, desconto/entrega não negativos e estoque suficiente.</summary>
public sealed class EditarVendaCommandValidator : AbstractValidator<EditarVendaCommand>
{
    public EditarVendaCommandValidator(AppDbContext db)
    {
        RuleFor(x => x.VendaId)
            .NotEmpty();

        RuleFor(x => x.VendaId)
            .MustAsync(async (id, cancellationToken) => await db.Vendas.AnyAsync(v => v.Id == id, cancellationToken))
            .WithMessage("A venda a editar não foi encontrada.");

        RuleFor(x => x.Itens)
            .NotEmpty().WithMessage("Adicione ao menos um produto à venda.");

        RuleForEach(x => x.Itens).ChildRules(item =>
        {
            item.RuleFor(i => i.Quantidade)
                .GreaterThan(0).WithMessage("A quantidade de cada item deve ser maior que zero.");

            item.RuleFor(i => i.PrecoUnitario)
                .GreaterThan(0).WithMessage("O preço unitário do item deve ser maior que zero.")
                .When(i => i.PrecoUnitario is not null);
        });

        RuleFor(x => x.Desconto)
            .GreaterThanOrEqualTo(0).WithMessage("O desconto não pode ser negativo.");

        RuleFor(x => x.ValorEntrega)
            .GreaterThanOrEqualTo(0).WithMessage("O valor da entrega não pode ser negativo.");

        // Cada produto deve ser válido e ter estoque suficiente para a quantidade.
        RuleForEach(x => x.Itens).MustAsync(async (command, item, _, cancellationToken) =>
        {
            var produto = await db.Produtos.FindAsync([item.ProdutoId], cancellationToken);
            return produto is { Ativo: true } && produto.QuantidadeEstoque >= item.Quantidade;
        }).WithMessage("Estoque insuficiente, produto desabilitado ou não encontrado para um dos itens.");
    }
}