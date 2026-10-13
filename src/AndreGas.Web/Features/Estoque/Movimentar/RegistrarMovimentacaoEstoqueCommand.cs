using AndreGas.Domain.Enums;

using Mediator;

namespace AndreGas.Web.Features.Estoque.Movimentar;

/// <summary>
/// Registra uma movimentação de estoque (entrada, saída ou ajuste) de um produto.
/// Em produtos que <see cref="Produto.ControlaVasilhame"/>, a entrada também soma vasilhames
/// (cada cheio comprado traz um vazio como veículo) e a saída devolve o vazio ao depósito;
/// <paramref name="AdicionarVasilhames"/> marca o prompt da tela para somar ainda mais vazios
/// (ex.: quando os vazios recebidos excedem a quantidade de cheios).
/// </summary>
public sealed record RegistrarMovimentacaoEstoqueCommand(
    Guid ProdutoId,
    TipoMovimentacaoEstoque Tipo,
    int Quantidade,
    string? Motivo,
    bool AdicionarVasilhames = false,
    int QuantidadeVasilhamesAdicionar = 0) : ICommand<bool>;