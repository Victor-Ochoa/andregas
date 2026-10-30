using RevendaApp.Web.Features.ConfiguracaoEmpresa;

namespace RevendaApp.UnitTests.Features.ConfiguracaoEmpresa;

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
        string? endereco = null,
        string? numero = null,
        string? cep = null,
        string? bairro = null,
        string? cidade = null,
        string? estado = null,
        IReadOnlyList<string>? telefones = null,
        string? corPrimaria = null,
        string? logo = null) =>
        new(nome, cnpj, endereco, numero, cep, bairro, cidade, estado, telefones, corPrimaria, logo);

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

    /// <summary>
    /// Os limites espelham <c>EmpresaConfiguration</c>. Sem eles, um valor mais longo que a coluna
    /// passa na validação e estoura no Postgres (22001) — o admin perde o formulário.
    /// </summary>
    [Theory]
    [InlineData(nameof(EditarConfigEmpresaCommand.Endereco), 300)]
    [InlineData(nameof(EditarConfigEmpresaCommand.Numero), 20)]
    [InlineData(nameof(EditarConfigEmpresaCommand.Bairro), 100)]
    [InlineData(nameof(EditarConfigEmpresaCommand.Cidade), 100)]
    [InlineData(nameof(EditarConfigEmpresaCommand.Cnpj), 20)]
    [InlineData(nameof(EditarConfigEmpresaCommand.Cep), 10)]
    public async Task Validator_rejeita_campo_acima_do_limite_da_coluna(string campo, int limite)
    {
        var validator = new EditarConfigEmpresaCommandValidator();

        // O CNPJ/CEP têm regra de formato: usa um valor de dígitos válido com o tamanho excedente.
        var excedente = campo switch
        {
            nameof(EditarConfigEmpresaCommand.Cnpj) => new string('1', limite + 1),
            nameof(EditarConfigEmpresaCommand.Cep) => new string('1', limite + 1),
            _ => new string('x', limite + 1),
        };

        var invalido = await validator.ValidateAsync(CampoCom(campo, excedente));
        Assert.Contains(invalido.Errors, e => e.PropertyName == campo);

        // Controle positivo: exatamente no limite é aceito (para CNPJ/CEP usa um valor válido).
        var noLimite = campo switch
        {
            nameof(EditarConfigEmpresaCommand.Cnpj) => "12345678000199",
            nameof(EditarConfigEmpresaCommand.Cep) => "01001000",
            _ => new string('x', limite),
        };

        var valido = await validator.ValidateAsync(CampoCom(campo, noLimite));
        Assert.DoesNotContain(valido.Errors, e => e.PropertyName == campo);
    }

    /// <summary>
    /// A coluna `Telefones` é uma string única (2000 chars) com os números separados por ";".
    /// Muitos telefones passariam como lista válida e estourariam na gravação.
    /// </summary>
    [Fact]
    public async Task Validator_rejeita_telefones_acima_do_limite_da_coluna()
    {
        var validator = new EditarConfigEmpresaCommandValidator();

        // 200 números de 11 dígitos = 200*11 + 199 separadores = 2399 chars > 2000.
        var muitosTelefones = Enumerable.Range(0, 200).Select(i => $"1199999{i:D4}").ToList();
        var invalido = await validator.ValidateAsync(Comando(telefones: muitosTelefones));
        Assert.Contains(invalido.Errors, e => e.PropertyName == nameof(EditarConfigEmpresaCommand.Telefones));

        // Controle positivo: uma lista normal passa.
        var valido = await validator.ValidateAsync(Comando(telefones: ["11999990000", "1133334444"]));
        Assert.DoesNotContain(valido.Errors, e => e.PropertyName == nameof(EditarConfigEmpresaCommand.Telefones));
    }

    private static EditarConfigEmpresaCommand CampoCom(string campo, string valor) => campo switch
    {
        nameof(EditarConfigEmpresaCommand.Cnpj) => Comando(cnpj: valor),
        nameof(EditarConfigEmpresaCommand.Endereco) => Comando(endereco: valor),
        nameof(EditarConfigEmpresaCommand.Numero) => Comando(numero: valor),
        nameof(EditarConfigEmpresaCommand.Cep) => Comando(cep: valor),
        nameof(EditarConfigEmpresaCommand.Bairro) => Comando(bairro: valor),
        nameof(EditarConfigEmpresaCommand.Cidade) => Comando(cidade: valor),
        _ => throw new ArgumentOutOfRangeException(nameof(campo), campo, "Campo sem regra de limite no teste."),
    };

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