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
        string? endereco = null,
        string? numero = null,
        string? cep = null,
        string? bairro = null,
        string? cidade = null) =>
        new(nome, identificador, email, senha, nomeAdmin, cnpj, endereco, numero, cep, bairro, cidade, null, telefones, null, null);

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
    /// Os limites de tamanho espelham <c>EmpresaConfiguration</c> (os mesmos de
    /// <see cref="AndreGas.Web.Features.ConfiguracaoEmpresa.EditarConfigEmpresaCommandValidator"/>):
    /// sem eles, um valor mais longo que a coluna passa na validação, chega ao Postgres e estoura
    /// <c>22001 value too long</c> — um <c>DbUpdateException</c> que o diálogo da central não sabe
    /// explicar e que derrubava o circuito com o formulário perdido.
    /// </summary>
    [Theory]
    [InlineData(nameof(CriarEmpresaCommand.Cnpj), 20)]
    [InlineData(nameof(CriarEmpresaCommand.Endereco), 300)]
    [InlineData(nameof(CriarEmpresaCommand.Numero), 20)]
    [InlineData(nameof(CriarEmpresaCommand.Cep), 10)]
    [InlineData(nameof(CriarEmpresaCommand.Bairro), 100)]
    [InlineData(nameof(CriarEmpresaCommand.Cidade), 100)]
    public async Task Validator_rejeita_campo_acima_do_limite_da_coluna(string campo, int limite)
    {
        using var db = InMemoryDbContextFactory.Create();
        var validator = new CriarEmpresaCommandValidator(db);

        // CNPJ/CEP: só dígitos, para que a única violação possível seja o tamanho.
        var excedente = campo is nameof(CriarEmpresaCommand.Cnpj) or nameof(CriarEmpresaCommand.Cep)
            ? new string('1', limite + 1)
            : new string('x', limite + 1);

        var invalido = await validator.ValidateAsync(CampoCom(campo, excedente));
        Assert.Contains(invalido.Errors, e => e.PropertyName == campo);

        // Controle positivo: exatamente no limite é aceito.
        var noLimite = campo switch
        {
            nameof(CriarEmpresaCommand.Cnpj) => "12345678000199",
            nameof(CriarEmpresaCommand.Cep) => "01001000",
            _ => new string('x', limite),
        };

        var valido = await validator.ValidateAsync(CampoCom(campo, noLimite));
        Assert.DoesNotContain(valido.Errors, e => e.PropertyName == campo);
    }

    /// <summary>A coluna `Telefones` é uma string única de 2000 chars separada por ";".</summary>
    [Fact]
    public async Task Validator_rejeita_telefones_acima_do_limite_da_coluna()
    {
        using var db = InMemoryDbContextFactory.Create();
        var validator = new CriarEmpresaCommandValidator(db);

        // 200 números de 11 dígitos = 200*11 + 199 separadores = 2399 chars > 2000.
        var muitosTelefones = Enumerable.Range(0, 200).Select(i => $"1199999{i:D4}").ToList();

        var invalido = await validator.ValidateAsync(Comando(telefones: muitosTelefones));
        Assert.Contains(invalido.Errors, e => e.PropertyName == nameof(CriarEmpresaCommand.Telefones));

        // Controle positivo: uma lista normal passa.
        var valido = await validator.ValidateAsync(Comando(telefones: ["11999990000", "1133334444"]));
        Assert.DoesNotContain(valido.Errors, e => e.PropertyName == nameof(CriarEmpresaCommand.Telefones));
    }

    private static CriarEmpresaCommand CampoCom(string campo, string valor) => campo switch
    {
        nameof(CriarEmpresaCommand.Cnpj) => Comando(cnpj: valor),
        nameof(CriarEmpresaCommand.Endereco) => Comando(endereco: valor),
        nameof(CriarEmpresaCommand.Numero) => Comando(numero: valor),
        nameof(CriarEmpresaCommand.Cep) => Comando(cep: valor),
        nameof(CriarEmpresaCommand.Bairro) => Comando(bairro: valor),
        nameof(CriarEmpresaCommand.Cidade) => Comando(cidade: valor),
        _ => throw new ArgumentOutOfRangeException(nameof(campo), campo, "Campo sem regra de limite no teste."),
    };

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