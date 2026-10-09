using AndreGas.Domain.Entities;
using AndreGas.Infrastructure;

using FluentValidation;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Clientes.Cadastrar;

public sealed class CadastrarClienteCommandValidator : AbstractValidator<CadastrarClienteCommand>
{
    public CadastrarClienteCommandValidator(AppDbContext db)
    {
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Informe o nome do cliente.")
            .MaximumLength(200);

        RuleFor(x => x.Telefone)
            .NotEmpty().WithMessage("Informe o telefone do cliente.")
            .MustAsync(async (telefone, cancellationToken) =>
            {
                var normalizado = Cliente.NormalizarTelefone(telefone);
                return !await db.Clientes.AnyAsync(c => c.Telefone == normalizado, cancellationToken);
            })
            .WithMessage("Já existe um cliente cadastrado com esse telefone.");

        RuleFor(x => x.Endereco)
            .NotEmpty().WithMessage("Informe o endereço do cliente.")
            .MaximumLength(300);
    }
}