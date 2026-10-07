using AndreGas.Domain.Enums;

namespace AndreGas.Domain.Entities;

/// <summary>
/// Registro de auditoria do estoque de um produto: movimentações, vendas, edições de valores e
/// cadastro, com motivo, usuário (quem) e data (quando). É usado apenas para exibição — os dados
/// operacionais de estoque continuam em <see cref="MovimentacaoEstoque"/>.
/// </summary>
public class HistoricoEstoque
{
    public Guid Id { get; private set; }
    public Guid ProdutoId { get; private set; }
    public Produto? Produto { get; private set; }
    public DateTime Data { get; private set; }

    public TipoHistoricoEstoque Tipo { get; private set; }

    /// <summary>Descrição legível do evento (ex.: "Venda de 2 produtos" ou "Preço de venda: 100,00 → 120,00").</summary>
    public string? Descricao { get; private set; }

    /// <summary>Origem do evento (motivo digitado, "Venda", "Cadastro inicial", "Edição de dados").</summary>
    public string? Motivo { get; private set; }

    /// <summary>Nome do usuário autenticado que executou a ação; "sistema" quando não autenticado.</summary>
    public string Usuario { get; private set; } = "sistema";

    /// <summary>Quantidade movimentada ou vendida; zero em cadastro; nulo em edição.</summary>
    public int? Quantidade { get; private set; }

    /// <summary>Vínculo opcional com a venda que baixou o estoque (apenas registro, sem FK).</summary>
    public Guid? VendaId { get; private set; }

    private HistoricoEstoque()
    {
    }

    public HistoricoEstoque(
        Guid produtoId,
        TipoHistoricoEstoque tipo,
        string? descricao,
        string? motivo,
        string usuario,
        int? quantidade = null,
        Guid? vendaId = null,
        DateTime? data = null)
    {
        Id = Guid.NewGuid();
        ProdutoId = produtoId;
        Tipo = tipo;
        Descricao = descricao;
        Motivo = motivo;
        Usuario = string.IsNullOrWhiteSpace(usuario) ? "sistema" : usuario.Trim();
        Quantidade = quantidade;
        VendaId = vendaId;
        Data = data ?? DateTime.UtcNow;
    }
}