using AndreGas.Infrastructure.Identity;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Central.UsuariosEmpresa;

namespace AndreGas.UnitTests.Features.Central;

/// <summary>
/// Testes do validador de <see cref="CriarUsuarioEmpresaCommand"/>: a central só pode criar
/// <c>Admin</c>/<c>Vendedor</c> (nunca <c>SuperAdmin</c>) e a unicidade de e-mail é a do Identity
/// (<c>NormalizedEmail</c>, case-insensitive).
/// </summary>
public class CriarUsuarioEmpresaValidatorTests
{
    private static CriarUsuarioEmpresaCommand Comando(
        string email = "vendedor@andregas.com.br",
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
            UserName = "vendedor@andregas.com.br",
            Email = "vendedor@andregas.com.br",
            NormalizedEmail = "VENDEDOR@ANDREGAS.COM.BR",
            NormalizedUserName = "VENDEDOR@ANDREGAS.COM.BR",
        });
        await db.SaveChangesAsync();

        var validator = new CriarUsuarioEmpresaCommandValidator(db);

        var resultado = await validator.ValidateAsync(Comando(email: "Vendedor@AndreGas.com.br"));

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e =>
            e.PropertyName == nameof(CriarUsuarioEmpresaCommand.Email)
            && e.ErrorMessage.Contains("Já existe", StringComparison.Ordinal));

        var valido = await validator.ValidateAsync(Comando(email: "novo@andregas.com.br"));
        Assert.DoesNotContain(valido.Errors, e => e.PropertyName == nameof(CriarUsuarioEmpresaCommand.Email));
    }
}