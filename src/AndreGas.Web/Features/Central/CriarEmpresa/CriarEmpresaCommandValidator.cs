using AndreGas.Domain.Entities;
using AndreGas.Infrastructure;

using FluentValidation;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Central.CriarEmpresa;

/// <summary>
/// Valida o cadastro de empresa feito pela central: nome, identificador (slug imutável), dados do
/// primeiro administrador (e-mail, senha, nome) e o formato dos campos opcionais quando informados.
/// </summary>
/// <remarks>
/// As regras de unicidade (identificador e e-mail) também são reforçadas no handler: o validador
/// dá a mensagem amigável na UI e o handler cobre corrida entre dois super admins.
/// </remarks>
public sealed class CriarEmpresaCommandValidator : AbstractValidator<CriarEmpresaCommand>
{
    /// <summary>Slug do identificador: minúsculas, dígitos e hífens simples entre os grupos.</summary>
    public const string PadraoIdentificador = @"^[a-z0-9]+(?:-[a-z0-9]+)*$";

    /// <summary>
    /// Limite da coluna `Telefones`: os números são persistidos como uma string única separada por
    /// ";" (ver o <c>ValueComparer</c> em `EmpresaConfiguration`).
    /// </summary>
    private const int LimiteTelefones = 2000;

    public CriarEmpresaCommandValidator(AppDbContext db)
    {
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Informe o nome da empresa.")
            .MaximumLength(200).WithMessage("O nome da empresa deve ter no máximo 200 caracteres.");

        RuleFor(x => x.Identificador)
            .NotEmpty().WithMessage("Informe o identificador da empresa.")
            .MaximumLength(100).WithMessage("O identificador deve ter no máximo 100 caracteres.")
            .Matches(PadraoIdentificador)
            .WithMessage("O identificador deve conter apenas letras minúsculas, números e hífens (ex.: andre-gas).")
            .MustAsync(async (identificador, cancellationToken) =>
                string.IsNullOrWhiteSpace(identificador)
                || !await db.Set<Empresa>().AnyAsync(e => e.Identificador == identificador.Trim(), cancellationToken))
            .WithMessage("Já existe uma empresa com esse identificador.");

        RuleFor(x => x.NomeAdmin)
            .NotEmpty().WithMessage("Informe o nome do administrador da empresa.")
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Informe o e-mail do administrador.")
            .EmailAddress().WithMessage("Informe um e-mail válido.")
            .MaximumLength(256)
            .MustAsync(async (email, cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(email))
                {
                    return true;
                }

                // NormalizedEmail: o Identity grava o e-mail normalizado (maiúsculas) e considera
                // unicidade por essa coluna. Comparar com Email seria case-sensitive e deixaria
                // passar "Admin@X" quando já existe "admin@x" — o erro só apareceria no Identity.
                var normalizado = email.Trim().ToUpperInvariant();
                return !await db.Users.AnyAsync(u => u.NormalizedEmail == normalizado, cancellationToken);
            })
            .WithMessage("Já existe um usuário cadastrado com esse e-mail.");

        RuleFor(x => x.Senha)
            .NotEmpty().WithMessage("Informe a senha do administrador.")
            .MinimumLength(6).WithMessage("A senha deve ter pelo menos 6 caracteres.");

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