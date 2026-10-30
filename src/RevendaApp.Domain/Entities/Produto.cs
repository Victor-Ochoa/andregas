using RevendaApp.Domain.Enums;

namespace RevendaApp.Domain.Entities;

/// <summary>
/// Produto vendido pela revendedora (botijões de gás, água mineral, etc.).
/// </summary>
public class Produto
{
    public Guid Id { get; private set; }

    /// <summary>Empresa (tenant) dona deste produto.</summary>
    public Guid EmpresaId { get; private set; }

    public string Nome { get; private set; } = string.Empty;
    public TipoProduto Tipo { get; private set; }
    public decimal PrecoVenda { get; private set; }
    public decimal PrecoCusto { get; private set; }

    /// <summary>Preço subsidiado usado quando a forma de pagamento é "Gás do Povo".</summary>
    public decimal PrecoGasDoPovo { get; private set; }

    public int QuantidadeEstoque { get; private set; }
    public int EstoqueMinimo { get; private set; }

    /// <summary>
    /// Indica se o produto está ativo no catálogo. Produto desabilitado não aparece na tela de
    /// nova venda e não pode ser vendido, mas permanece na listagem de estoque para reativação.
    /// </summary>
    public bool Ativo { get; private set; }

    /// <summary>
    /// Indica se o produto controla vasilhames (vasilhames cheios devolvidos/produto retorna vazio).
    /// </summary>
    public bool ControlaVasilhame { get; private set; }

    /// <summary>
    /// Indica se o produto participa do aviso de recompra na Home: quando marcado, o sistema
    /// acompanha a recorrência de compra por cliente (via <see cref="RecorrenciaCompra"/>) e
    /// sugere ligar para o cliente quando a data prevista de nova compra é atingida.
    /// </summary>
    public bool AvisarRecompra { get; private set; }

    /// <summary>
    /// Intervalo padrão (em dias) de recompra para este produto, usado como valor inicial do
    /// aprendizado por cliente enquanto ainda não há histórico suficiente.
    /// </summary>
    public int DiasPadraoRecompra { get; private set; }

    /// <summary>Quantidade de vasilhames vazios/disponíveis em estoque.</summary>
    public int QuantidadeVasilhames { get; private set; }

    /// <summary>Quantidade de vasilhames emprestados a clientes (ainda não devolvidos).</summary>
    public int QuantidadeVasilhamesEmprestados { get; private set; }

    /// <summary>Total de vasilhames em circulação (disponíveis + emprestados).</summary>
    public int TotalVasilhames => QuantidadeVasilhames + QuantidadeVasilhamesEmprestados;

    public bool EstoqueBaixo => QuantidadeEstoque <= EstoqueMinimo;

    private Produto()
    {
    }

    public Produto(string nome, TipoProduto tipo, decimal precoVenda, decimal precoCusto, decimal precoGasDoPovo, int estoqueMinimo = 0, bool controlaVasilhame = false, bool avisarRecompra = false, int diasPadraoRecompra = 0)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("O nome do produto é obrigatório.", nameof(nome));
        }

        if (precoVenda < 0 || precoCusto < 0 || precoGasDoPovo < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(precoVenda), "Os preços não podem ser negativos.");
        }

        if (estoqueMinimo < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(estoqueMinimo), "O estoque mínimo não pode ser negativo.");
        }

        if (diasPadraoRecompra < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(diasPadraoRecompra), "Os dias padrão de recompra não podem ser negativos.");
        }

        Id = Guid.NewGuid();
        Nome = nome.Trim();
        Tipo = tipo;
        PrecoVenda = precoVenda;
        PrecoCusto = precoCusto;
        PrecoGasDoPovo = precoGasDoPovo;
        EstoqueMinimo = estoqueMinimo;
        QuantidadeEstoque = 0;
        Ativo = true;
        ControlaVasilhame = controlaVasilhame;
        QuantidadeVasilhames = 0;
        QuantidadeVasilhamesEmprestados = 0;
        AvisarRecompra = avisarRecompra;
        DiasPadraoRecompra = diasPadraoRecompra;
    }

    /// <summary>Preço unitário a aplicar de acordo com a forma de pagamento da venda.</summary>
    public decimal PrecoParaFormaPagamento(FormaPagamento formaPagamento) =>
        formaPagamento == FormaPagamento.GasDoPovo ? PrecoGasDoPovo : PrecoVenda;

    public void AtualizarDados(string nome, TipoProduto tipo, decimal precoVenda, decimal precoCusto, decimal precoGasDoPovo, int estoqueMinimo, bool controlaVasilhame = false, bool avisarRecompra = false, int diasPadraoRecompra = 0)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("O nome do produto é obrigatório.", nameof(nome));
        }

        if (precoVenda < 0 || precoCusto < 0 || precoGasDoPovo < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(precoVenda), "Os preços não podem ser negativos.");
        }

        if (diasPadraoRecompra < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(diasPadraoRecompra), "Os dias padrão de recompra não podem ser negativos.");
        }

        Nome = nome.Trim();
        Tipo = tipo;
        PrecoVenda = precoVenda;
        PrecoCusto = precoCusto;
        PrecoGasDoPovo = precoGasDoPovo;
        EstoqueMinimo = estoqueMinimo;
        ControlaVasilhame = controlaVasilhame;
        AvisarRecompra = avisarRecompra;
        DiasPadraoRecompra = diasPadraoRecompra;
    }

    /// <summary>Marca o produto como inativo (não aparece na nova venda).</summary>
    public void Desativar() => Ativo = false;

    /// <summary>Marca o produto como ativo no catálogo.</summary>
    public void Ativar() => Ativo = true;

    public void RegistrarEntrada(int quantidade)
    {
        if (quantidade <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantidade), "A quantidade de entrada deve ser maior que zero.");
        }

        QuantidadeEstoque += quantidade;
    }

    public void RegistrarSaida(int quantidade)
    {
        if (quantidade <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantidade), "A quantidade de saída deve ser maior que zero.");
        }

        if (quantidade > QuantidadeEstoque)
        {
            throw new InvalidOperationException($"Estoque insuficiente de '{Nome}': disponível {QuantidadeEstoque}, solicitado {quantidade}.");
        }

        QuantidadeEstoque -= quantidade;
    }

    /// <summary>
    /// Baixa produtos cheios para um item marcado como <b>empréstimo</b> de vasilhame. Diferente de
    /// <see cref="RegistrarSaida"/>, permite o estoque cheio ficar <b>negativo</b>: o déficit é coberto
    /// por vasilhames já emprestados ao cliente (o Validator garante <c>Estoque + Emprestados do
    /// cliente ≥ Qtd</c>) e a reversão da venda (<see cref="RegistrarDevolucaoVasilhame"/> + Estoque)
    /// restaura o saldo.
    /// </summary>
    public void RegistrarSaidaEmprestimo(int quantidade)
    {
        if (quantidade <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantidade), "A quantidade de saída deve ser maior que zero.");
        }

        QuantidadeEstoque -= quantidade;
    }

    /// <summary>Registra a entrada de vasilhames vazios no estoque.</summary>
    public void RegistrarEntradaVasilhame(int quantidade)
    {
        if (quantidade <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantidade), "A quantidade de entrada de vasilhames deve ser maior que zero.");
        }

        QuantidadeVasilhames += quantidade;
    }

    /// <summary>Registra a saída de vasilhames (disponíveis).</summary>
    public void RegistrarSaidaVasilhame(int quantidade)
    {
        if (quantidade <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantidade), "A quantidade de saída de vasilhames deve ser maior que zero.");
        }

        if (quantidade > QuantidadeVasilhames)
        {
            throw new ArgumentOutOfRangeException(nameof(quantidade), $"Vasilhames disponíveis insuficientes: disponível {QuantidadeVasilhames}, solicitado {quantidade}.");
        }

        QuantidadeVasilhames -= quantidade;
    }

    /// <summary>Registra vasilhames emprestados a um cliente (soma aos emprestados).</summary>
    public void RegistrarEmprestimoVasilhame(int quantidade)
    {
        if (quantidade <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantidade), "A quantidade de vasilhames emprestados deve ser maior que zero.");
        }

        QuantidadeVasilhamesEmprestados += quantidade;
    }

    /// <summary>Registra devolução de vasilhames emprestados (diminui os emprestados).</summary>
    public void RegistrarDevolucaoVasilhame(int quantidade)
    {
        if (quantidade <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantidade), "A quantidade de vasilhames devolvidos deve ser maior que zero.");
        }

        if (quantidade > QuantidadeVasilhamesEmprestados)
        {
            throw new ArgumentOutOfRangeException(nameof(quantidade), $"Devolução excede vasilhames emprestados: emprestados {QuantidadeVasilhamesEmprestados}, devolução {quantidade}.");
        }

        QuantidadeVasilhamesEmprestados -= quantidade;
    }

    public void AjustarEstoque(int novaQuantidade)
    {
        if (novaQuantidade < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(novaQuantidade), "A quantidade em estoque não pode ser negativa.");
        }

        QuantidadeEstoque = novaQuantidade;
    }

    /// <summary>Define a quantidade de vasilhames disponíveis (vazios) para um valor exato.</summary>
    public void AjustarVasilhames(int novaQuantidade)
    {
        if (novaQuantidade < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(novaQuantidade), "A quantidade de vasilhames disponíveis não pode ser negativa.");
        }

        QuantidadeVasilhames = novaQuantidade;
    }
}