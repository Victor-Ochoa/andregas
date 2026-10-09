using AndreGas.Infrastructure;

using FluentValidation;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Estoque.Movimentar;

public sealed class RegistrarMovimentacaoEstoqueCommandValidator : AbstractValidator<RegistrarMovimentacaoEstoqueCommand>
{
    public RegistrarMovimentacaoEstoqueCommandValidator(AppDbContext db)
    {
        RuleFor(x => x.ProdutoId)
            .MustAsync(async (produtoId, cancellationToken) => await db.Produtos.AnyAsync(p => p.Id == produtoId, cancellationToken))
            .WithMessage("Produto não encontrado.");

        RuleFor(x => x.Tipo).IsInEnum();

        RuleFor(x => x.Quantidade)
            .GreaterThan(0).WithMessage("A quantidade deve ser maior que zero.");

        RuleFor(x => x.Motivo)
            .MaximumLength(300);
    }
}