using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Central.CriarEmpresa;

namespace AndreGas.UnitTests.Features.Central;

/// <summary>
/// Testes do validador de <see cref="CriarEmpresaCommand"/>: o cadastro da empresa pela central
/// cria, junto, o primeiro administrador — então os campos obrigatórios são Nome, Identificador
/// (slug imutável), E-mail/Senha/NomeAdmin, enquanto CNPJ, telefones e endereço são opcionais.
/// </summary>
public class CriarEmpresaValidatorTests
{
    private static CriarEmpresaCommand Comando(
        string nome = "André Gás",
        string identificador = "andre-gas",
        string email = "admin@andregas.com.br",
        string senha = "Senha@123",
        string nomeAdmin = "Administrador André",
        string? cnpj = null,
        IReadOnlyList<string>? telefones = null,
        string? endereco = null) =>
        new(nome, identificador, email, senha, nomeAdmin, cnpj, endereco, null, null, null, null, null, telefones, null, null);

    [Fact]
    public async Task Validator_rejeita_nome_vazio()
    {
        using var db = InMemoryDbContextFactory.Create();
        var validator = new CriarEmpresaCommandValidator(db);

        var resultado = await validator.ValidateAsync(Comando(nome: "   "));

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(CriarEmpresaCommand.Nome));
    }

    [Fact]
    public async Task Validator_rejeita_identificador_com_caracteres_invalidos()
    {
        using var db = InMemoryDbContextFactory.Create();
        var validator = new CriarEmpresaCommandValidator(db);

        // Maiúsculas, espaços e acento são inválidos: o identificador é um slug (ex.: "andre-gas").
        var resultado = await validator.ValidateAsync(Comando(identificador: "André Gás"));

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(CriarEmpresaCommand.Identificador));

        // Controle positivo: o mesmo comando com um slug válido passa nessa regra.
        var valido = await validator.ValidateAsync(Comando(identificador: "andre-gas"));
        Assert.DoesNotContain(valido.Errors, e => e.PropertyName == nameof(CriarEmpresaCommand.Identificador));
    }

    [Fact]
    public async Task Validator_rejeita_email_invalido()
    {
        using var db = InMemoryDbContextFactory.Create();
        var validator = new CriarEmpresaCommandValidator(db);

        var resultado = await validator.ValidateAsync(Comando(email: "nao-e-email"));

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(CriarEmpresaCommand.Email));
    }

    [Fact]
    public async Task Validator_rejeita_senha_curta()
    {
        using var db = InMemoryDbContextFactory.Create();
        var validator = new CriarEmpresaCommandValidator(db);

        var resultado = await validator.ValidateAsync(Comando(senha: "123"));

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(CriarEmpresaCommand.Senha));
    }

    [Fact]
    public async Task Validator_aceita_cnpj_telefones_e_endereco_opcionais()
    {
        using var db = InMemoryDbContextFactory.Create();
        var validator = new CriarEmpresaCommandValidator(db);

        // Sem CNPJ, sem telefones e sem endereço: continua válido (só o mínimo é obrigatório).
        var resultado = await validator.ValidateAsync(Comando(cnpj: null, telefones: null, endereco: null));

        Assert.True(resultado.IsValid, string.Join(" ", resultado.Errors.Select(e => e.ErrorMessage)));
    }

    /// <summary>
    /// A unicidade do e-mail é case-insensitive no Identity (comparada por <c>NormalizedEmail</c>).
    /// Uma checagem com <c>Email</c> deixaria passar "ADMIN@x" quando já existe "admin@x" e o erro
    /// só apareceria na criação do usuário, depois da empresa já inserida.
    /// </summary>
    [Fact]
    public async Task Validator_rejeita_email_ja_usado_ignorando_maiusculas()
    {
        using var db = InMemoryDbContextFactory.Create();

        // Cadastrado em minúsculas, com o NormalizedEmail que o Identity gravaria.
        db.Users.Add(new AndreGas.Infrastructure.Identity.ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "admin@andregas.com.br",
            Email = "admin@andregas.com.br",
            NormalizedEmail = "ADMIN@ANDREGAS.COM.BR",
            NormalizedUserName = "ADMIN@ANDREGAS.COM.BR",
        });
        await db.SaveChangesAsync();

        var validator = new CriarEmpresaCommandValidator(db);

        var resultado = await validator.ValidateAsync(Comando(email: "Admin@AndreGas.com.br"));

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e =>
            e.PropertyName == nameof(CriarEmpresaCommand.Email)
            && e.ErrorMessage.Contains("Já existe", StringComparison.Ordinal));

        // Controle positivo: um e-mail diferente continua válido.
        var valido = await validator.ValidateAsync(Comando(email: "outro@andregas.com.br"));
        Assert.DoesNotContain(valido.Errors, e => e.PropertyName == nameof(CriarEmpresaCommand.Email));
    }
}