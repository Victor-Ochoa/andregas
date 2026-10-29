using AndreGas.Web.Features.ConfiguracaoEmpresa;

namespace AndreGas.UnitTests.Features.ConfiguracaoEmpresa;

/// <summary>
/// Testes do validador de <see cref="EditarConfigEmpresaCommand"/>: o administrador da empresa edita
/// o próprio cadastro pela aba "Minha Empresa". Só o nome é obrigatório; CNPJ, CEP, UF, cor e logo
/// são opcionais, mas quando informados precisam ter formato válido.
/// </summary>
/// <remarks>
/// O comando <b>não</b> carrega <c>EmpresaId</c> (a empresa vem do tenant) nem
/// <c>Identificador</c> (imutável) — por isso o validador não tem regra de existência de empresa e
/// não precisa de <c>AppDbContext</c>.
/// </remarks>
public class EditarConfigEmpresaValidatorTests
{
    private static EditarConfigEmpresaCommand Comando(
        string nome = "André Gás",
        string? cnpj = null,
        string? cep = null,
        string? estado = null,
        IReadOnlyList<string>? telefones = null,
        string? corPrimaria = null,
        string? logo = null) =>
        new(nome, cnpj, null, null, cep, null, null, estado, telefones, corPrimaria, logo);

    [Fact]
    public async Task Validator_rejeita_nome_vazio()
    {
        var validator = new EditarConfigEmpresaCommandValidator();

        var resultado = await validator.ValidateAsync(Comando(nome: "   "));

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(EditarConfigEmpresaCommand.Nome));

        // Controle positivo: o mesmo comando com um nome preenchido passa nessa regra.
        var valido = await validator.ValidateAsync(Comando(nome: "André Gás e Água"));
        Assert.DoesNotContain(valido.Errors, e => e.PropertyName == nameof(EditarConfigEmpresaCommand.Nome));
    }

    [Fact]
    public async Task Validator_aceita_campos_opcionais_nulos()
    {
        var validator = new EditarConfigEmpresaCommandValidator();

        // Sem CNPJ, CEP, UF, telefones, cor ou logo: o comando continua válido.
        var resultado = await validator.ValidateAsync(Comando());

        Assert.True(resultado.IsValid, string.Join(" ", resultado.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task Validator_rejeita_cnpj_com_digitos_insuficientes()
    {
        var validator = new EditarConfigEmpresaCommandValidator();

        var resultado = await validator.ValidateAsync(Comando(cnpj: "123.456.789"));

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(EditarConfigEmpresaCommand.Cnpj));

        // Controle positivo: 14 dígitos passam nessa regra.
        var valido = await validator.ValidateAsync(Comando(cnpj: "12345678000199"));
        Assert.DoesNotContain(valido.Errors, e => e.PropertyName == nameof(EditarConfigEmpresaCommand.Cnpj));
    }

    [Fact]
    public async Task Validator_rejeita_cor_em_formato_invalido()
    {
        var validator = new EditarConfigEmpresaCommandValidator();

        // "azul" e "#12345" não são hex aceitos (#RGB ou #RRGGBB).
        var resultado = await validator.ValidateAsync(Comando(corPrimaria: "#12345"));

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(EditarConfigEmpresaCommand.CorPrimaria));

        // Controle positivo: as duas formas hex aceitas passam.
        foreach (var cor in new[] { "#0A1F44", "#fff" })
        {
            var valido = await validator.ValidateAsync(Comando(corPrimaria: cor));
            Assert.DoesNotContain(valido.Errors, e => e.PropertyName == nameof(EditarConfigEmpresaCommand.CorPrimaria));
        }
    }

    [Fact]
    public async Task Validator_rejeita_logo_acima_do_limite()
    {
        var validator = new EditarConfigEmpresaCommandValidator();

        // O limite do domínio/configuração é 700_000 caracteres (~500KB em base64).
        var resultado = await validator.ValidateAsync(Comando(logo: new string('a', 700_001)));

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(EditarConfigEmpresaCommand.Logo));

        // Controle positivo: exatamente no limite ainda é aceito.
        var valido = await validator.ValidateAsync(Comando(logo: new string('a', 700_000)));
        Assert.DoesNotContain(valido.Errors, e => e.PropertyName == nameof(EditarConfigEmpresaCommand.Logo));
    }

    [Fact]
    public async Task Validator_aceita_cnpj_e_cep_com_mascara()
    {
        var validator = new EditarConfigEmpresaCommandValidator();

        // A normalização é só de dígitos: máscara é aceita e o domínio grava o valor como veio.
        var resultado = await validator.ValidateAsync(
            Comando(cnpj: "12.345.678/0001-99", cep: "01001-000", estado: "SP"));

        Assert.True(resultado.IsValid, string.Join(" ", resultado.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task Validator_rejeita_cep_e_uf_invalidos()
    {
        var validator = new EditarConfigEmpresaCommandValidator();

        var cepInvalido = await validator.ValidateAsync(Comando(cep: "123"));
        Assert.Contains(cepInvalido.Errors, e => e.PropertyName == nameof(EditarConfigEmpresaCommand.Cep));

        var ufInvalida = await validator.ValidateAsync(Comando(estado: "SPX"));
        Assert.Contains(ufInvalida.Errors, e => e.PropertyName == nameof(EditarConfigEmpresaCommand.Estado));
    }
}