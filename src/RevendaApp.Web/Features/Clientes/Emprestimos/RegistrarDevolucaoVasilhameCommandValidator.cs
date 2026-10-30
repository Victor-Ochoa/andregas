using FluentValidation;

using Microsoft.EntityFrameworkCore;

using RevendaApp.Infrastructure;

namespace RevendaApp.Web.Features.Clientes.Emprestimos;

public sealed class RegistrarDevolucaoVasilhameCommandValidator : AbstractValidator<RegistrarDevolucaoVasilhameCommand>
{
    public RegistrarDevolucaoVasilhameCommandValidator(AppDbContext db)
    {
        RuleFor(x => x.ClienteId)
            .NotEmpty().WithMessage("Informe o cliente.")
            .MustAsync(async (clienteId, cancellationToken) => await db.Clientes.AnyAsync(c => c.Id == clienteId, cancellationToken))
            .WithMessage("O cliente não existe.");

        RuleFor(x => x.ProdutoId)
            .NotEmpty().WithMessage("Informe o produto.")
            .MustAsync(async (produtoId, cancellationToken) => await db.Produtos.AnyAsync(p => p.Id == produtoId, cancellationToken))
            .WithMessage("O produto não existe.");

        RuleFor(x => x.Quantidade)
            .GreaterThan(0).WithMessage("A quantidade deve ser maior que zero.");

        RuleFor(x => x.Observacao)
            .MaximumLength(300);
    }
}