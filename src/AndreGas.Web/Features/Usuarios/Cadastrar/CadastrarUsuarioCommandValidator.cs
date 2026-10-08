using AndreGas.Infrastructure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Usuarios.Cadastrar;

public sealed class CadastrarUsuarioCommandValidator : AbstractValidator<CadastrarUsuarioCommand>
{
    public CadastrarUsuarioCommandValidator(AppDbContext db)
    {
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Informe o nome do usuário.")
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Informe o e-mail do usuário.")
            .EmailAddress().WithMessage("Informe um e-mail válido.")
            .MaximumLength(256)
            .MustAsync(async (email, cancellationToken) =>
                !await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
            .WithMessage("Já existe um usuário cadastrado com esse e-mail.");

        RuleFor(x => x.Papel)
            .IsInEnum().WithMessage("Selecione um papel válido.");

        RuleFor(x => x.Senha)
            .NotEmpty().WithMessage("Informe a senha.")
            .MinimumLength(6).WithMessage("A senha deve ter pelo menos 6 caracteres.");

        RuleFor(x => x.ConfirmarSenha)
            .Equal(x => x.Senha).WithMessage("As senhas não conferem.");
    }
}