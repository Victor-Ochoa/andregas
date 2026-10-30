using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Identity;

using FluentValidation;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Central.UsuariosEmpresa;

/// <summary>
/// Valida a criação de usuário na central: dados básicos, papel permitido (somente
/// <c>Admin</c>/<c>Vendedor</c> — nunca <c>SuperAdmin</c> por esta tela) e unicidade de e-mail.
/// </summary>
public sealed class CriarUsuarioEmpresaCommandValidator : AbstractValidator<CriarUsuarioEmpresaCommand>
{
    /// <summary>Papéis que a central pode atribuir a um usuário de empresa.</summary>
    public static readonly IReadOnlyList<string> PapeisPermitidos = [ApplicationRole.Admin, ApplicationRole.Vendedor];

    public CriarUsuarioEmpresaCommandValidator(AppDbContext db)
    {
        RuleFor(x => x.EmpresaId)
            .NotEmpty().WithMessage("Informe a empresa do usuário.")
            .MustAsync(async (id, cancellationToken) =>
                await db.Set<AndreGas.Domain.Entities.Empresa>().AnyAsync(e => e.Id == id, cancellationToken))
            .WithMessage("Empresa não encontrada.");

        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Informe o nome do usuário.")
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Informe o e-mail do usuário.")
            .EmailAddress().WithMessage("Informe um e-mail válido.")
            .MaximumLength(256)
            .MustAsync(async (email, cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(email))
                {
                    return true;
                }

                // NormalizedEmail (não Email): a unicidade do Identity é case-insensitive, e uma
                // checagem case-sensitive deixaria passar "Admin@X" quando já existe "admin@x".
                var normalizado = email.Trim().ToUpperInvariant();
                return !await db.Users.AnyAsync(u => u.NormalizedEmail == normalizado, cancellationToken);
            })
            .WithMessage("Já existe um usuário cadastrado com esse e-mail.");

        RuleFor(x => x.Papel)
            .NotEmpty().WithMessage("Selecione o papel do usuário.")
            .Must(papel => PapeisPermitidos.Contains(papel))
            .WithMessage("O papel deve ser Administrador ou Vendedor.");

        RuleFor(x => x.Senha)
            .NotEmpty().WithMessage("Informe a senha.")
            .MinimumLength(6).WithMessage("A senha deve ter pelo menos 6 caracteres.");
    }
}