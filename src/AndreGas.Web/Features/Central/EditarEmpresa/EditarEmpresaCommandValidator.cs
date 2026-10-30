using AndreGas.Infrastructure;

using FluentValidation;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Central.EditarEmpresa;

/// <summary>
/// Valida a edição de empresa pela central. O identificador não está no comando (imutável); as
/// demais regras espelham o cadastro (formato dos campos opcionais) e a empresa precisa existir.
/// </summary>
/// <remarks>
/// <b>Os limites de tamanho espelham <c>EmpresaConfiguration</c></b> (e não são inventados aqui):
/// sem eles, um valor mais longo que a coluna passa na validação, chega ao Postgres e estoura com
/// <c>22001 value too long</c> — um <c>DbUpdateException</c> que o diálogo da central não sabia
/// explicar (ele só captura <c>ValidationException</c>). Mesmos limites da aba "Minha Empresa"
/// (<see cref="AndreGas.Web.Features.ConfiguracaoEmpresa.EditarConfigEmpresaCommandValidator"/>).
/// </remarks>
public sealed class EditarEmpresaCommandValidator : AbstractValidator<EditarEmpresaCommand>
{
    /// <summary>
    /// Limite da coluna `Telefones`: os números são persistidos como uma string única separada por
    /// ";" (ver o <c>ValueComparer</c> em `EmpresaConfiguration`).
    /// </summary>
    private const int LimiteTelefones = 2000;

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
            .MaximumLength(20).WithMessage("O CNPJ deve ter no máximo 20 caracteres.")
            .Must(cnpj => string.IsNullOrWhiteSpace(cnpj) || SomenteDigitos(cnpj).Length == 14)
            .WithMessage("O CNPJ deve ter 14 dígitos.");

        RuleFor(x => x.Endereco)
            .MaximumLength(300).WithMessage("O logradouro deve ter no máximo 300 caracteres.");

        RuleFor(x => x.Numero)
            .MaximumLength(20).WithMessage("O número deve ter no máximo 20 caracteres.");

        RuleFor(x => x.Cep)
            .MaximumLength(10).WithMessage("O CEP deve ter no máximo 10 caracteres.")
            .Must(cep => string.IsNullOrWhiteSpace(cep) || SomenteDigitos(cep).Length == 8)
            .WithMessage("O CEP deve ter 8 dígitos.");

        RuleFor(x => x.Bairro)
            .MaximumLength(100).WithMessage("O bairro deve ter no máximo 100 caracteres.");

        RuleFor(x => x.Cidade)
            .MaximumLength(100).WithMessage("A cidade deve ter no máximo 100 caracteres.");

        RuleFor(x => x.Estado)
            .Must(estado => string.IsNullOrWhiteSpace(estado) || (estado.Trim().Length == 2 && estado.Trim().All(char.IsAsciiLetter)))
            .WithMessage("A UF deve ter 2 letras.");

        RuleFor(x => x.Telefones)
            .Must(telefones => telefones is null
                || string.Join(";", telefones.Select(AndreGas.Domain.Entities.Empresa.NormalizarTelefone))
                    .Length <= LimiteTelefones)
            .WithMessage("A lista de telefones é longa demais.");

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