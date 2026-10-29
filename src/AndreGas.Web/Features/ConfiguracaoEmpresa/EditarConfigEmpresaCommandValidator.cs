using FluentValidation;

namespace AndreGas.Web.Features.ConfiguracaoEmpresa;

/// <summary>
/// Valida a edição da própria empresa (aba "Minha Empresa"). Só o nome é obrigatório; os demais
/// campos são opcionais, mas quando informados precisam ter formato válido.
/// </summary>
/// <remarks>
/// Não há regra de "empresa existe" nem <c>AppDbContext</c> aqui: o comando não carrega
/// <c>EmpresaId</c> — a empresa vem do tenant e o handler a carrega por esse id.
/// </remarks>
public sealed class EditarConfigEmpresaCommandValidator : AbstractValidator<EditarConfigEmpresaCommand>
{
    /// <summary>Limite do logo em caracteres (~500KB em base64), o mesmo do domínio/configuração.</summary>
    private const int LimiteLogo = 700_000;

    public EditarConfigEmpresaCommandValidator()
    {
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
            .Must(estado => string.IsNullOrWhiteSpace(estado)
                || (estado.Trim().Length == 2 && estado.Trim().All(char.IsAsciiLetter)))
            .WithMessage("A UF deve ter 2 letras.");

        RuleFor(x => x.CorPrimaria)
            .Matches("^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$")
            .When(x => !string.IsNullOrWhiteSpace(x.CorPrimaria))
            .WithMessage("Informe a cor no formato hexadecimal (ex.: #0A1F44).");

        RuleFor(x => x.Logo)
            .Must(logo => logo is null || logo.Length <= LimiteLogo)
            .WithMessage("O logo deve ter no máximo ~500KB.");
    }

    private static string SomenteDigitos(string valor) => new(valor.Where(char.IsDigit).ToArray());
}