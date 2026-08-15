using AndreGas.Domain.Entities;
using AndreGas.Infrastructure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Clientes.Detalhe;

public sealed class AtualizarClienteCommandValidator : AbstractValidator<AtualizarClienteCommand>
{
    public AtualizarClienteCommandValidator(AppDbContext db)
    {
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Informe o nome do cliente.")
            .MaximumLength(200);

        RuleFor(x => x.Telefone)
            .NotEmpty().WithMessage("Informe o telefone do cliente.")
            .MustAsync(async (command, telefone, cancellationToken) =>
            {
                var normalizado = Cliente.NormalizarTelefone(telefone);
                return !await db.Clientes.AnyAsync(c => c.Telefone == normalizado && c.Id != command.Id, cancellationToken);
            })
            .WithMessage("Já existe outro cliente cadastrado com esse telefone.");

        RuleFor(x => x.Endereco)
            .NotEmpty().WithMessage("Informe o endereço do cliente.")
            .MaximumLength(300);
    }
}
