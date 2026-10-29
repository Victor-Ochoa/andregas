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
}