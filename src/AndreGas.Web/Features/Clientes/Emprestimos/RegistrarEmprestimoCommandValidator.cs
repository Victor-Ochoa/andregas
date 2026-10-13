using AndreGas.Infrastructure;

using FluentValidation;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Clientes.Emprestimos;

public sealed class RegistrarEmprestimoCommandValidator : AbstractValidator<RegistrarEmprestimoCommand>
{
    public RegistrarEmprestimoCommandValidator(AppDbContext db)
    {
        RuleFor(x => x.ClienteId)
            .NotEmpty().WithMessage("Informe o cliente.")
            .MustAsync(async (clienteId, cancellationToken) => await db.Clientes.AnyAsync(c => c.Id == clienteId, cancellationToken))
            .WithMessage("O cliente não existe.");

        RuleFor(x => x.ProdutoId)
            .NotEmpty().WithMessage("Informe o produto.")
            .MustAsync(async (produtoId, cancellationToken) =>
            {
                var produto = await db.Produtos.FindAsync([produtoId], cancellationToken);
                return produto is not null && produto.ControlaVasilhame;
            })
            .WithMessage("O produto deve existir e controlar vasilhames.");

        RuleFor(x => x.Quantidade)
            .GreaterThan(0).WithMessage("A quantidade deve ser maior que zero.");

        RuleFor(x => x.Observacao)
            .MaximumLength(300);
    }
}