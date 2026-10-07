using AndreGas.Infrastructure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Estoque.Editar;

public sealed class AtualizarProdutoCommandValidator : AbstractValidator<AtualizarProdutoCommand>
{
    public AtualizarProdutoCommandValidator(AppDbContext db)
    {
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Informe o nome do produto.")
            .MaximumLength(200);

        RuleFor(x => x.Tipo).IsInEnum();

        RuleFor(x => x.PrecoVenda)
            .GreaterThanOrEqualTo(0).WithMessage("O preço de venda não pode ser negativo.");

        RuleFor(x => x.PrecoCusto)
            .GreaterThanOrEqualTo(0).WithMessage("O preço de custo não pode ser negativo.");

        RuleFor(x => x.PrecoGasDoPovo)
            .GreaterThanOrEqualTo(0).WithMessage("O preço do Gás do Povo não pode ser negativo.");

        RuleFor(x => x.EstoqueMinimo)
            .GreaterThanOrEqualTo(0).WithMessage("O estoque mínimo não pode ser negativo.");

        RuleFor(x => x.ProdutoId)
            .NotEmpty();

        // Só atualiza um produto que existe.
        RuleFor(x => x.ProdutoId)
            .MustAsync(async (id, cancellationToken) => await db.Produtos.AnyAsync(p => p.Id == id, cancellationToken))
            .WithMessage("O produto a editar não foi encontrado.");
    }
}