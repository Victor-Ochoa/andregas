using RevendaApp.UnitTests.TestHelpers;
using RevendaApp.Web.Features.Central.EditarEmpresa;

namespace RevendaApp.UnitTests.Features.Central;

/// <summary>
/// Testes do validador de <see cref="EditarEmpresaCommand"/> (edição de empresa pela central).
/// </summary>
/// <remarks>
/// <para>
/// O foco aqui é o <b>tamanho</b> dos campos opcionais: o editor da central aceita os mesmos campos
/// da aba "Minha Empresa", e sem <c>MaximumLength</c> um valor mais longo que a coluna passa na
/// validação, chega ao Postgres e estoura <c>22001 value too long</c>. O diálogo só captura
/// <c>ValidationException</c>, então esse <c>DbUpdateException</c> derrubava o circuito com o
/// formulário perdido (finding 1 da Task 11, que não tinha sido propagado para a central).
/// </para>
/// <para>
/// Os limites espelham <c>EmpresaConfiguration</c> — os mesmos de
/// <see cref="RevendaApp.Web.Features.ConfiguracaoEmpresa.EditarConfigEmpresaCommandValidator"/>.
/// </para>
/// </remarks>
public class EditarEmpresaValidatorTests
{
    private static EditarEmpresaCommand Comando(
        Guid? empresaId = null,
        string nome = "Empresa A",
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
        new(empresaId ?? Guid.NewGuid(), nome, cnpj, endereco, numero, cep, bairro, cidade, estado,
            telefones, corPrimaria, logo);

    /// <summary>
    /// Cada campo opcional recusa <c>limite + 1</c> e aceita exatamente <c>limite</c>. O controle
    /// positivo (no limite) impede o teste de passar por vacuidade com uma regra que rejeita tudo.
    /// </summary>
    [Theory]
    [InlineData(nameof(EditarEmpresaCommand.Cnpj), 20)]
    [InlineData(nameof(EditarEmpresaCommand.Endereco), 300)]
    [InlineData(nameof(EditarEmpresaCommand.Numero), 20)]
    [InlineData(nameof(EditarEmpresaCommand.Cep), 10)]
    [InlineData(nameof(EditarEmpresaCommand.Bairro), 100)]
    [InlineData(nameof(EditarEmpresaCommand.Cidade), 100)]
    public async Task Validator_rejeita_campo_acima_do_limite_da_coluna(string campo, int limite)
    {
        using var db = InMemoryDbContextFactory.Create();
        var empresa = await CriarEmpresaAsync(db);
        var validator = new EditarEmpresaCommandValidator(db);

        // CNPJ/CEP têm regra de formato: o valor de limite+1 chars não pode ser confundido com
        // "formato inválido" — usa só dígitos para que a única violação seja o tamanho.
        var excedente = campo is nameof(EditarEmpresaCommand.Cnpj) or nameof(EditarEmpresaCommand.Cep)
            ? new string('1', limite + 1)
            : new string('x', limite + 1);

        var invalido = await validator.ValidateAsync(CampoCom(campo, excedente, empresa.Id));
        Assert.Contains(invalido.Errors, e => e.PropertyName == campo);

        // Controle positivo: exatamente no limite é aceito (CNPJ/CEP usam valores de formato válido).
        var noLimite = campo switch
        {
            nameof(EditarEmpresaCommand.Cnpj) => "12345678000199",
            nameof(EditarEmpresaCommand.Cep) => "01001000",
            _ => new string('x', limite),
        };

        var valido = await validator.ValidateAsync(CampoCom(campo, noLimite, empresa.Id));
        Assert.DoesNotContain(valido.Errors, e => e.PropertyName == campo);
    }

    /// <summary>
    /// A coluna `Telefones` é uma string única (2000 chars) com os números separados por ";".
    /// Muitos telefones passariam como lista válida e estourariam na gravação.
    /// </summary>
    [Fact]
    public async Task Validator_rejeita_telefones_acima_do_limite_da_coluna()
    {
        using var db = InMemoryDbContextFactory.Create();
        var empresa = await CriarEmpresaAsync(db);
        var validator = new EditarEmpresaCommandValidator(db);

        // 200 números de 11 dígitos = 200*11 + 199 separadores = 2399 chars > 2000.
        var muitosTelefones = Enumerable.Range(0, 200).Select(i => $"1199999{i:D4}").ToList();

        var invalido = await validator.ValidateAsync(Comando(empresaId: empresa.Id, telefones: muitosTelefones));
        Assert.Contains(invalido.Errors, e => e.PropertyName == nameof(EditarEmpresaCommand.Telefones));

        // Controle positivo: uma lista normal passa.
        var valido = await validator.ValidateAsync(Comando(empresaId: empresa.Id, telefones: ["11999990000", "1133334444"]));
        Assert.DoesNotContain(valido.Errors, e => e.PropertyName == nameof(EditarEmpresaCommand.Telefones));
    }

    [Fact]
    public async Task Validator_aceita_campos_opcionais_nulos()
    {
        using var db = InMemoryDbContextFactory.Create();
        var empresa = await CriarEmpresaAsync(db);
        var validator = new EditarEmpresaCommandValidator(db);

        var resultado = await validator.ValidateAsync(Comando(empresaId: empresa.Id));

        Assert.True(resultado.IsValid, string.Join(" ", resultado.Errors.Select(e => e.ErrorMessage)));
    }

    /// <summary>
    /// Persiste uma empresa mínima: o validador de edição exige que a empresa exista (diferente do
    /// validador de "Minha Empresa", onde a empresa vem do tenant).
    /// </summary>
    private static async Task<RevendaApp.Domain.Entities.Empresa> CriarEmpresaAsync(RevendaApp.Infrastructure.AppDbContext db)
    {
        var empresa = new RevendaApp.Domain.Entities.Empresa($"empresa-{Guid.NewGuid():N}", "Empresa A");
        db.Set<RevendaApp.Domain.Entities.Empresa>().Add(empresa);
        await db.SaveChangesAsync();
        return empresa;
    }

    private static EditarEmpresaCommand CampoCom(string campo, string valor, Guid empresaId) => campo switch
    {
        nameof(EditarEmpresaCommand.Cnpj) => Comando(empresaId: empresaId, cnpj: valor),
        nameof(EditarEmpresaCommand.Endereco) => Comando(empresaId: empresaId, endereco: valor),
        nameof(EditarEmpresaCommand.Numero) => Comando(empresaId: empresaId, numero: valor),
        nameof(EditarEmpresaCommand.Cep) => Comando(empresaId: empresaId, cep: valor),
        nameof(EditarEmpresaCommand.Bairro) => Comando(empresaId: empresaId, bairro: valor),
        nameof(EditarEmpresaCommand.Cidade) => Comando(empresaId: empresaId, cidade: valor),
        _ => throw new ArgumentOutOfRangeException(nameof(campo), campo, "Campo sem regra de limite no teste."),
    };
}