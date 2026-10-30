using RevendaApp.Infrastructure.Identity;
using RevendaApp.UnitTests.TestHelpers;
using RevendaApp.Web.Features.Central.UsuariosEmpresa;

namespace RevendaApp.UnitTests.Features.Central;

/// <summary>
/// Testes do validador de <see cref="CriarUsuarioEmpresaCommand"/>: a central só pode criar
/// <c>Admin</c>/<c>Vendedor</c> (nunca <c>SuperAdmin</c>) e a unicidade de e-mail é a do Identity
/// (<c>NormalizedEmail</c>, case-insensitive).
/// </summary>
public class CriarUsuarioEmpresaValidatorTests
{
    private static CriarUsuarioEmpresaCommand Comando(
        string email = "vendedor@revenda.app.com.br",
        string papel = ApplicationRole.Vendedor,
        string nome = "Vendedor Teste",
        string senha = "Senha@123") =>
        new(Guid.NewGuid(), nome, email, papel, senha);

    [Fact]
    public async Task Validator_rejeita_papel_super_admin()
    {
        using var db = InMemoryDbContextFactory.Create();
        var validator = new CriarUsuarioEmpresaCommandValidator(db);

        var resultado = await validator.ValidateAsync(Comando(papel: ApplicationRole.SuperAdmin));

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(CriarUsuarioEmpresaCommand.Papel));

        // Controle positivo: Admin e Vendedor passam nessa regra.
        foreach (var papel in new[] { ApplicationRole.Admin, ApplicationRole.Vendedor })
        {
            var valido = await validator.ValidateAsync(Comando(papel: papel));
            Assert.DoesNotContain(valido.Errors, e => e.PropertyName == nameof(CriarUsuarioEmpresaCommand.Papel));
        }
    }

    /// <summary>
    /// A unicidade do e-mail é case-insensitive no Identity: comparar com <c>Email</c> deixaria
    /// passar "VENDEDOR@x" quando já existe "vendedor@x".
    /// </summary>
    [Fact]
    public async Task Validator_rejeita_email_ja_usado_ignorando_maiusculas()
    {
        using var db = InMemoryDbContextFactory.Create();
        db.Users.Add(new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "vendedor@revenda.app.com.br",
            Email = "vendedor@revenda.app.com.br",
            NormalizedEmail = "VENDEDOR@REVENDAAPP.COM.BR",
            NormalizedUserName = "VENDEDOR@REVENDAAPP.COM.BR",
        });
        await db.SaveChangesAsync();

        var validator = new CriarUsuarioEmpresaCommandValidator(db);

        var resultado = await validator.ValidateAsync(Comando(email: "Vendedor@RevendaApp.com.br"));

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e =>
            e.PropertyName == nameof(CriarUsuarioEmpresaCommand.Email)
            && e.ErrorMessage.Contains("Já existe", StringComparison.Ordinal));

        var valido = await validator.ValidateAsync(Comando(email: "novo@revenda.app.com.br"));
        Assert.DoesNotContain(valido.Errors, e => e.PropertyName == nameof(CriarUsuarioEmpresaCommand.Email));
    }
}