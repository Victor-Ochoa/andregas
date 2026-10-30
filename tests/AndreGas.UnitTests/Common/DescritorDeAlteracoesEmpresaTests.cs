using AndreGas.Domain.Entities;
using AndreGas.Web.Common.Empresas;

namespace AndreGas.UnitTests.Common;

/// <summary>
/// O descritor alimenta a trilha de auditoria. Ele precisa distinguir "algo mudou" de "salvou sem
/// mexer em nada": gravar um registro "Nenhuma alteração…" enche a trilha de eventos que não
/// aconteceram e esconde os que aconteceram.
/// </summary>
public class DescritorDeAlteracoesEmpresaTests
{
    [Fact]
    public void Descrever_DeveRetornarNull_QuandoNadaMudou()
    {
        var empresa = CriarEmpresa();

        var detalhes = DescritorDeAlteracoesEmpresa.Descrever(
            DescritorDeAlteracoesEmpresa.Retratar(empresa),
            DescritorDeAlteracoesEmpresa.Retratar(empresa));

        Assert.Null(detalhes);
    }

    /// <summary>
    /// Controle positivo: uma alteração real continua produzindo o resumo antes → depois. Sem ele
    /// o teste acima passaria mesmo que o descritor parasse de descrever qualquer coisa.
    /// </summary>
    [Fact]
    public void Descrever_DeveDescreverCampoAlterado()
    {
        var antes = DescritorDeAlteracoesEmpresa.Retratar(CriarEmpresa());
        var depoisEmpresa = CriarEmpresa();
        depoisEmpresa.AtualizarDados("André Gás Renomeada", estado: "RJ");

        var detalhes = DescritorDeAlteracoesEmpresa.Descrever(
            antes, DescritorDeAlteracoesEmpresa.Retratar(depoisEmpresa));

        Assert.NotNull(detalhes);
        Assert.Contains("Nome: André Gás → André Gás Renomeada", detalhes, StringComparison.Ordinal);
        Assert.Contains("UF: SP → RJ", detalhes, StringComparison.Ordinal);
    }

    /// <summary>Telefones são coleção: reordenar não é alteração, adicionar é.</summary>
    [Fact]
    public void Descrever_DeveDetectarAlteracaoNosTelefones()
    {
        var antes = DescritorDeAlteracoesEmpresa.Retratar(CriarEmpresa());
        var depoisEmpresa = CriarEmpresa();
        depoisEmpresa.AtualizarDados("André Gás", telefones: ["11999990000", "1133334444"], estado: "SP");

        var detalhes = DescritorDeAlteracoesEmpresa.Descrever(
            antes, DescritorDeAlteracoesEmpresa.Retratar(depoisEmpresa));

        Assert.NotNull(detalhes);
        Assert.Contains("Telefones", detalhes, StringComparison.Ordinal);
    }

    private static Empresa CriarEmpresa() =>
        new("andre-gas", "André Gás", ["11999990000"], endereco: "Rua A", numero: "10",
            cep: "01001000", bairro: "Centro", cidade: "São Paulo", estado: "SP");
}