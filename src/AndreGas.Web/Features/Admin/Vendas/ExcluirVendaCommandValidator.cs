using AndreGas.Infrastructure;

using FluentValidation;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Admin.Vendas;

/// <summary>Valida o comando de exclusão de venda: a venda deve existir.</summary>
public sealed class ExcluirVendaCommandValidator : AbstractValidator<ExcluirVendaCommand>
{
    public ExcluirVendaCommandValidator(AppDbContext db)
    {
        RuleFor(x => x.VendaId)
            .NotEmpty();

        RuleFor(x => x.VendaId)
            .MustAsync(async (id, cancellationToken) => await db.Vendas.AnyAsync(v => v.Id == id, cancellationToken))
            .WithMessage("A venda a excluir não foi encontrada.");
    }
}