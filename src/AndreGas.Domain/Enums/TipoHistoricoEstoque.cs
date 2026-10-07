namespace AndreGas.Domain.Enums;

/// <summary>
/// Tipo de evento registrado no histórico de estoque de um produto (auditoria/exibição).
/// </summary>
public enum TipoHistoricoEstoque
{
    Entrada,
    Saida,
    Ajuste,
    Venda,
    Edicao,
    Cadastro,
}