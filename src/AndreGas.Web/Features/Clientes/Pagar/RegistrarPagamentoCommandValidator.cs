using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;

using FluentValidation;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Clientes.Pagar;

public sealed class RegistrarPagamentoCommandValidator : AbstractValidator<RegistrarPagamentoCommand>
{
    public RegistrarPagamentoCommandValidator(AppDbContext db)
    {
        RuleFor(x => x.ClienteId)
            .NotEmpty().WithMessage("Informe o cliente.");

        RuleFor(x => x.Valor)
            .GreaterThan(0).WithMessage("O valor do pagamento deve ser maior que zero.");

        RuleFor(x => x.FormaPagamento)
            .IsInEnum().WithMessage("Forma de pagamento inválida.")
            .NotEqual(FormaPagamento.Fiado).WithMessage("A forma de pagamento de um pagamento de saldo não pode ser Fiado.");

        RuleFor(x => x.Valor)
            .MustAsync(async (command, valor, cancellationToken) =>
            {
                var cliente = await db.Clientes.FindAsync([command.ClienteId], cancellationToken);
                return cliente is not null && valor <= cliente.SaldoDevedor;
            })
            .WithMessage("O valor do pagamento não pode ser maior que o saldo devedor do cliente.");
    }
}