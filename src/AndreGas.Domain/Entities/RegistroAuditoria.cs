namespace AndreGas.Domain.Entities;

/// <summary>
/// Registro de auditoria de uma ação relevante (edição de dados da empresa, criação/inativação de
/// empresa ou usuário, "entrar como", ...). É uma entidade <b>global</b>: não pertence
/// exclusivamente a uma empresa, porque a central precisa lê-la de todas e cada empresa precisa ler
/// as suas.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por isso <see cref="EmpresaId"/> é anulável e não há filtro global por empresa nem
/// preenchimento automático pelo interceptor</b>: <c>null</c> significa "ação da central" (super
/// admin sem empresa) e o valor é atribuído explicitamente por quem grava o registro.
/// </para>
/// <para>
/// <see cref="Acao"/> e <see cref="Origem"/> são <b>strings</b> (não enum) por decisão de design: a
/// lista de ações cresce a cada funcionalidade e o banco guarda o valor legível. Use as constantes
/// de <see cref="Acoes"/> e <see cref="Origens"/> para evitar literais soltos.
/// </para>
/// </remarks>
public class RegistroAuditoria
{
    /// <summary>Tamanho máximo de <see cref="Detalhes"/> (o resumo é truncado antes de gravar).</summary>
    public const int TamanhoMaximoDetalhes = 2000;

    /// <summary>Rótulo usado quando não há usuário autenticado (seed, tarefa de sistema).</summary>
    public const string UsuarioSistema = "sistema";

    /// <summary>Ações auditadas. Cresce conforme novas funcionalidades gravam auditoria.</summary>
    public static class Acoes
    {
        /// <summary>O administrador da empresa editou os dados dela pela aba "Minha Empresa".</summary>
        public const string ConfigEmpresaEditada = "ConfigEmpresaEditada";
    }

    /// <summary>Origem do registro: de onde a ação partiu.</summary>
    public static class Origens
    {
        /// <summary>Ação executada dentro do app da empresa.</summary>
        public const string App = "App";

        /// <summary>Ação executada pela central do super admin.</summary>
        public const string Central = "Central";
    }

    public Guid Id { get; private set; }

    /// <summary>Empresa afetada; <c>null</c> = ação da central (sem empresa selecionada).</summary>
    public Guid? EmpresaId { get; private set; }

    /// <summary>Id do usuário autenticado que executou a ação; <c>null</c> quando não resolvido.</summary>
    public Guid? UsuarioId { get; private set; }

    /// <summary>Nome (e-mail) do usuário autenticado; "sistema" quando não autenticado.</summary>
    public string UsuarioNome { get; private set; } = UsuarioSistema;

    /// <summary>Ação executada — uma das constantes de <see cref="Acoes"/>.</summary>
    public string Acao { get; private set; } = string.Empty;

    /// <summary>Resumo legível do que mudou (em pt-BR, com "antes → depois"); truncado em 2000 chars.</summary>
    public string Detalhes { get; private set; } = string.Empty;

    /// <summary>Momento da ação, em UTC.</summary>
    public DateTime DataHora { get; private set; }

    /// <summary>Origem da ação — uma das constantes de <see cref="Origens"/>.</summary>
    public string Origem { get; private set; } = string.Empty;

    /// <summary>Construtor exigido pelo EF Core.</summary>
    private RegistroAuditoria()
    {
    }

    public RegistroAuditoria(
        Guid? empresaId,
        Guid? usuarioId,
        string? usuarioNome,
        string acao,
        string detalhes,
        string origem)
    {
        if (string.IsNullOrWhiteSpace(acao))
        {
            throw new ArgumentException("A ação auditada é obrigatória.", nameof(acao));
        }

        if (string.IsNullOrWhiteSpace(origem))
        {
            throw new ArgumentException("A origem do registro de auditoria é obrigatória.", nameof(origem));
        }

        Id = Guid.NewGuid();
        EmpresaId = empresaId;
        UsuarioId = usuarioId;
        UsuarioNome = string.IsNullOrWhiteSpace(usuarioNome) ? UsuarioSistema : usuarioNome.Trim();
        Acao = acao.Trim();
        Detalhes = Truncar(detalhes);
        DataHora = DateTime.UtcNow;
        Origem = origem.Trim();
    }

    /// <summary>
    /// Corta o resumo no limite da coluna. O corte é feito aqui (e não só no handler) para que
    /// nenhum caminho consiga gravar um texto maior do que a coluna aceita.
    /// </summary>
    private static string Truncar(string? detalhes)
    {
        if (string.IsNullOrEmpty(detalhes))
        {
            return string.Empty;
        }

        return detalhes.Length <= TamanhoMaximoDetalhes
            ? detalhes
            : detalhes[..TamanhoMaximoDetalhes];
    }
}