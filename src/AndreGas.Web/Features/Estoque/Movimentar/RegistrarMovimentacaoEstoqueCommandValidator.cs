using AndreGas.Domain.Enums;
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

        // Prompt de vasilhames na entrada: com a flag marcada, a quantidade adicional de vazios
        // precisa ser maior que zero (e só faz sentido em entrada com controle de vasilhame).
        RuleFor(x => x.QuantidadeVasilhamesAdicionar)
            .GreaterThan(0)
            .WithMessage("Para adicionar vasilhames na entrada, informe uma quantidade maior que zero.")
            .When(x => x.AdicionarVasilhames && x.Tipo == TipoMovimentacaoEstoque.Entrada);
    }
}