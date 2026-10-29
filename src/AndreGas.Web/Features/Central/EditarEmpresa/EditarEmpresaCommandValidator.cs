using AndreGas.Infrastructure;

using FluentValidation;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Central.EditarEmpresa;

/// <summary>
/// Valida a edição de empresa pela central. O identificador não está no comando (imutável); as
/// demais regras espelham o cadastro (formato dos campos opcionais) e a empresa precisa existir.
/// </summary>
public sealed class EditarEmpresaCommandValidator : AbstractValidator<EditarEmpresaCommand>
{
    public EditarEmpresaCommandValidator(AppDbContext db)
    {
        RuleFor(x => x.EmpresaId)
            .NotEmpty()
            .MustAsync(async (id, cancellationToken) =>
                await db.Set<AndreGas.Domain.Entities.Empresa>().AnyAsync(e => e.Id == id, cancellationToken))
            .WithMessage("A empresa a editar não foi encontrada.");

        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Informe o nome da empresa.")
            .MaximumLength(200).WithMessage("O nome da empresa deve ter no máximo 200 caracteres.");

        RuleFor(x => x.Cnpj)
            .Must(cnpj => string.IsNullOrWhiteSpace(cnpj) || SomenteDigitos(cnpj).Length == 14)
            .WithMessage("O CNPJ deve ter 14 dígitos.");

        RuleFor(x => x.Cep)
            .Must(cep => string.IsNullOrWhiteSpace(cep) || SomenteDigitos(cep).Length == 8)
            .WithMessage("O CEP deve ter 8 dígitos.");

        RuleFor(x => x.Estado)
            .Must(estado => string.IsNullOrWhiteSpace(estado) || (estado.Trim().Length == 2 && estado.Trim().All(char.IsAsciiLetter)))
            .WithMessage("A UF deve ter 2 letras.");

        RuleFor(x => x.CorPrimaria)
            .Matches("^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$")
            .When(x => !string.IsNullOrWhiteSpace(x.CorPrimaria))
            .WithMessage("Informe a cor no formato hexadecimal (ex.: #0A1F44).");

        RuleFor(x => x.Logo)
            .Must(logo => logo is null || logo.Length <= 700_000)
            .WithMessage("O logo deve ter no máximo ~500KB.");
    }

    private static string SomenteDigitos(string valor) => new(valor.Where(char.IsDigit).ToArray());
}