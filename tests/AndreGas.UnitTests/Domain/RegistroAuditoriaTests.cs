using AndreGas.Domain.Entities;

namespace AndreGas.UnitTests.Domain;

/// <summary>
/// Testes da entidade <see cref="RegistroAuditoria"/>: entidade <b>global</b> (EmpresaId anulável,
/// <c>null</c> = ação da central), com ação/origem obrigatórias e detalhes truncados no limite da
/// coluna.
/// </summary>
public class RegistroAuditoriaTests
{
    [Fact]
    public void Registro_criado_preenche_Id_DataHora_UTC_e_UsuarioSistema_quando_sem_usuario()
    {
        var antes = DateTime.UtcNow;

        var registro = new RegistroAuditoria(
            empresaId: null,
            usuarioId: null,
            usuarioNome: "   ",
            RegistroAuditoria.Acoes.ConfigEmpresaEditada,
            "Nome: A → B",
            RegistroAuditoria.Origens.Central);

        Assert.NotEqual(Guid.Empty, registro.Id);
        Assert.Null(registro.EmpresaId); // ação da central
        Assert.Null(registro.UsuarioId);
        Assert.Equal(RegistroAuditoria.UsuarioSistema, registro.UsuarioNome);
        Assert.Equal(RegistroAuditoria.Acoes.ConfigEmpresaEditada, registro.Acao);
        Assert.Equal(RegistroAuditoria.Origens.Central, registro.Origem);
        Assert.True(registro.DataHora >= antes);
        Assert.True(registro.DataHora <= DateTime.UtcNow);
    }

    [Fact]
    public void Registro_preserva_empresa_e_usuario_quando_informados()
    {
        var empresaId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();

        var registro = new RegistroAuditoria(
            empresaId,
            usuarioId,
            "admin@andregas.com.br",
            RegistroAuditoria.Acoes.ConfigEmpresaEditada,
            "Nome: A → B",
            RegistroAuditoria.Origens.App);

        Assert.Equal(empresaId, registro.EmpresaId);
        Assert.Equal(usuarioId, registro.UsuarioId);
        Assert.Equal("admin@andregas.com.br", registro.UsuarioNome);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Registro_rejeita_acao_vazia(string acao)
    {
        Assert.Throws<ArgumentException>(() => new RegistroAuditoria(
            Guid.NewGuid(), null, "admin", acao, "detalhes", RegistroAuditoria.Origens.App));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Registro_rejeita_origem_vazia(string origem)
    {
        Assert.Throws<ArgumentException>(() => new RegistroAuditoria(
            Guid.NewGuid(), null, "admin", RegistroAuditoria.Acoes.ConfigEmpresaEditada, "detalhes", origem));
    }

    [Fact]
    public void Registro_trunca_detalhes_no_limite_da_coluna()
    {
        var detalhesGrandes = new string('x', RegistroAuditoria.TamanhoMaximoDetalhes + 500);

        var registro = new RegistroAuditoria(
            Guid.NewGuid(), null, "admin",
            RegistroAuditoria.Acoes.ConfigEmpresaEditada, detalhesGrandes, RegistroAuditoria.Origens.App);

        Assert.Equal(RegistroAuditoria.TamanhoMaximoDetalhes, registro.Detalhes.Length);

        // Controle positivo: um texto dentro do limite não é cortado.
        var dentroDoLimite = new string('y', RegistroAuditoria.TamanhoMaximoDetalhes);
        var registroOk = new RegistroAuditoria(
            Guid.NewGuid(), null, "admin",
            RegistroAuditoria.Acoes.ConfigEmpresaEditada, dentroDoLimite, RegistroAuditoria.Origens.App);

        Assert.Equal(dentroDoLimite, registroOk.Detalhes);
    }

    /// <summary>
    /// O corte não pode partir um par surrogate (emoji): gravar meio par produz um texto inválido
    /// no Postgres. Quando o limite cai no meio do par, o corte recua um caractere.
    /// </summary>
    [Fact]
    public void Registro_nao_parte_par_surrogate_ao_truncar()
    {
        // Um emoji ocupa 2 chars UTF-16. Preenche até que o limite caia exatamente no MEIO do par.
        var prefixo = new string('x', RegistroAuditoria.TamanhoMaximoDetalhes - 1);
        var detalhes = prefixo + "\U0001F600" + new string('y', 100);

        var registro = new RegistroAuditoria(
            Guid.NewGuid(), null, "admin",
            RegistroAuditoria.Acoes.ConfigEmpresaEditada, detalhes, RegistroAuditoria.Origens.App);

        // Recuou um char para não cortar o par: 1999 chars, terminando no último 'x'.
        Assert.Equal(RegistroAuditoria.TamanhoMaximoDetalhes - 1, registro.Detalhes.Length);
        Assert.DoesNotContain(registro.Detalhes, char.IsSurrogate);
        Assert.Equal(prefixo, registro.Detalhes);
    }

    /// <summary>
    /// <c>UsuarioNome</c> tem 200 chars, mas o e-mail do Identity aceita até 256. Como a escrita da
    /// auditoria é atômica com a edição, um e-mail longo estouraria a coluna e derrubaria a edição
    /// inteira. O nome é truncado (o <c>UsuarioId</c> continua sendo a identidade real).
    /// </summary>
    [Fact]
    public void Registro_trunca_usuario_nome_no_limite_da_coluna()
    {
        var emailLongo = new string('a', 250) + "@andregas.com.br";

        var registro = new RegistroAuditoria(
            Guid.NewGuid(), Guid.NewGuid(), emailLongo,
            RegistroAuditoria.Acoes.ConfigEmpresaEditada, "detalhes", RegistroAuditoria.Origens.App);

        Assert.Equal(RegistroAuditoria.TamanhoMaximoUsuarioNome, registro.UsuarioNome.Length);

        // Controle positivo: um e-mail normal não é cortado.
        var registroOk = new RegistroAuditoria(
            Guid.NewGuid(), Guid.NewGuid(), "admin@andregas.com.br",
            RegistroAuditoria.Acoes.ConfigEmpresaEditada, "detalhes", RegistroAuditoria.Origens.App);

        Assert.Equal("admin@andregas.com.br", registroOk.UsuarioNome);
    }
}