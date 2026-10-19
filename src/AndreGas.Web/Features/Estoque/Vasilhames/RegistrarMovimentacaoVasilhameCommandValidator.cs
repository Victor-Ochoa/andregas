using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;

using FluentValidation;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Estoque.Vasilhames;

public sealed class RegistrarMovimentacaoVasilhameCommandValidator : AbstractValidator<RegistrarMovimentacaoVasilhameCommand>
{
    public RegistrarMovimentacaoVasilhameCommandValidator(AppDbContext db)
    {
        RuleFor(x => x.ProdutoId)
            .NotEmpty().WithMessage("Informe o produto.")
            .MustAsync(async (produtoId, cancellationToken) =>
            {
                var produto = await db.Produtos.FindAsync([produtoId], cancellationToken);
                return produto is not null && produto.ControlaVasilhame;
            })
            .WithMessage("O produto deve existir e controlar vasilhames.");

        RuleFor(x => x.Tipo).IsInEnum();

        // Ajuste pode zerar os disponíveis (inventário/fim de estoque); entrada/saída exigem > 0.
        RuleFor(x => x.Quantidade)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Tipo == TipoMovimentacaoEstoque.Ajuste)
            .WithMessage("A quantidade de vasilhames não pode ser negativa.");

        RuleFor(x => x.Quantidade)
            .GreaterThan(0)
            .When(x => x.Tipo != TipoMovimentacaoEstoque.Ajuste)
            .WithMessage("A quantidade deve ser maior que zero.");

        RuleFor(x => x.Observacao)
            .MaximumLength(300);
    }
}