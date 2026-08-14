namespace AndreGas.Domain.Entities;

/// <summary>
/// Cliente da revendedora. O telefone é a chave natural usada para localizar o cliente na
/// tela de venda.
/// </summary>
public class Cliente
{
    public Guid Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string Telefone { get; private set; } = string.Empty;
    public string Endereco { get; private set; } = string.Empty;
    public decimal SaldoDevedor { get; private set; }
    public DateTime CriadoEm { get; private set; }
    public bool Ativo { get; private set; }

    /// <summary>Construtor exigido pelo EF Core.</summary>
    private Cliente()
    {
    }

    public Cliente(string nome, string telefone, string endereco, DateTime? criadoEm = null)
    {
        Id = Guid.NewGuid();
        SetNome(nome);
        SetTelefone(telefone);
        SetEndereco(endereco);
        SaldoDevedor = 0m;
        CriadoEm = criadoEm ?? DateTime.UtcNow;
        Ativo = true;
    }

    public void AtualizarDados(string nome, string telefone, string endereco)
    {
        SetNome(nome);
        SetTelefone(telefone);
        SetEndereco(endereco);
    }

    /// <summary>Soma o valor de uma venda fiado ao saldo devedor do cliente.</summary>
    public void AdicionarSaldoDevedor(decimal valor)
    {
        if (valor < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valor), "O valor a adicionar não pode ser negativo.");
        }

        SaldoDevedor += valor;
    }

    /// <summary>Abate um pagamento do saldo devedor do cliente.</summary>
    public void RegistrarPagamento(decimal valor)
    {
        if (valor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valor), "O valor do pagamento deve ser maior que zero.");
        }

        SaldoDevedor = Math.Max(0m, SaldoDevedor - valor);
    }

    public void Desativar() => Ativo = false;

    public void Ativar() => Ativo = true;

    /// <summary>Remove tudo que não for dígito, para comparar telefones de forma consistente.</summary>
    public static string NormalizarTelefone(string telefone) =>
        new(telefone.Where(char.IsDigit).ToArray());

    private void SetNome(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("O nome do cliente é obrigatório.", nameof(nome));
        }

        Nome = nome.Trim();
    }

    private void SetTelefone(string telefone)
    {
        if (string.IsNullOrWhiteSpace(telefone))
        {
            throw new ArgumentException("O telefone do cliente é obrigatório.", nameof(telefone));
        }

        var normalizado = NormalizarTelefone(telefone);
        if (normalizado.Length == 0)
        {
            throw new ArgumentException("O telefone do cliente é inválido.", nameof(telefone));
        }

        Telefone = normalizado;
    }

    private void SetEndereco(string endereco)
    {
        if (string.IsNullOrWhiteSpace(endereco))
        {
            throw new ArgumentException("O endereço do cliente é obrigatório.", nameof(endereco));
        }

        Endereco = endereco.Trim();
    }
}
