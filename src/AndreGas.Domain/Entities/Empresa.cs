namespace AndreGas.Domain.Entities;

/// <summary>
/// Empresa (tenant) dona dos dados de domínio. Cada empresa tem um <see cref="Identificador"/>
/// único e imutável (slug amigável) e um nome exibido no app bar. As demais entidades de domínio
/// pertencem a uma empresa via <c>EmpresaId</c>.
/// </summary>
public class Empresa
{
    private readonly List<string> _telefones = [];

    public Guid Id { get; private set; }

    /// <summary>Slug amigável e único da empresa (ex.: "andre", "demo"). Imutável após a criação.</summary>
    public string Identificador { get; private set; } = string.Empty;

    /// <summary>Nome exibido no app bar da empresa.</summary>
    public string Nome { get; private set; } = string.Empty;

    /// <summary>CNPJ da empresa (opcional).</summary>
    public string? Cnpj { get; private set; }

    /// <summary>Logradouro (opcional).</summary>
    public string? Endereco { get; private set; }

    /// <summary>Número (opcional).</summary>
    public string? Numero { get; private set; }

    /// <summary>CEP (opcional).</summary>
    public string? Cep { get; private set; }

    /// <summary>Bairro (opcional).</summary>
    public string? Bairro { get; private set; }

    /// <summary>Cidade (opcional).</summary>
    public string? Cidade { get; private set; }

    /// <summary>UF (opcional). Será usado em features futuras (ex.: entrega, geolocalização).</summary>
    public string? Estado { get; private set; }

    /// <summary>Telefones de contato, cada um normalizado para apenas dígitos.</summary>
    public IReadOnlyCollection<string> Telefones => _telefones.AsReadOnly();

    /// <summary>Cor primária da empresa (hex, ex.: "#0A1F44"); null = azul marinho atual.</summary>
    public string? CorPrimaria { get; private set; }

    /// <summary>Imagem (base64 data-uri), opcional, limite ~500KB.</summary>
    public string? Logo { get; private set; }

    /// <summary>Indica se a empresa está ativa (empresa inativa não loga).</summary>
    public bool Ativo { get; private set; }

    public DateTime CriadaEm { get; private set; }

    /// <summary>Construtor exigido pelo EF Core.</summary>
    private Empresa()
    {
    }

    public Empresa(string identificador, string nome, IEnumerable<string>? telefones = null, string? cnpj = null, string? endereco = null, string? numero = null, string? cep = null, string? bairro = null, string? cidade = null, string? estado = null, string? corPrimaria = null, string? logo = null)
    {
        if (string.IsNullOrWhiteSpace(identificador))
        {
            throw new ArgumentException("O identificador da empresa é obrigatório.", nameof(identificador));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("O nome da empresa é obrigatório.", nameof(nome));
        }

        Id = Guid.NewGuid();
        Identificador = identificador.Trim();
        Nome = nome.Trim();
        Cnpj = NormalizarOpcional(cnpj);
        Endereco = NormalizarOpcional(endereco);
        Numero = NormalizarOpcional(numero);
        Cep = NormalizarOpcional(cep);
        Bairro = NormalizarOpcional(bairro);
        Cidade = NormalizarOpcional(cidade);
        Estado = NormalizarOpcional(estado);
        SetTelefones(telefones);
        CorPrimaria = NormalizarOpcional(corPrimaria);
        Logo = NormalizarOpcional(logo);
        Ativo = true;
        CriadaEm = DateTime.UtcNow;
    }

    public void Desativar() => Ativo = false;

    public void Ativar() => Ativo = true;

    /// <summary>
    /// Atualiza os dados de cadastro da empresa. O <see cref="Identificador"/> nunca é alterado.
    /// </summary>
    public void AtualizarDados(string nome, string? cnpj = null, string? endereco = null, string? numero = null, string? cep = null, string? bairro = null, string? cidade = null, string? estado = null, IEnumerable<string>? telefones = null, string? corPrimaria = null, string? logo = null)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("O nome da empresa é obrigatório.", nameof(nome));
        }

        Nome = nome.Trim();
        Cnpj = NormalizarOpcional(cnpj);
        Endereco = NormalizarOpcional(endereco);
        Numero = NormalizarOpcional(numero);
        Cep = NormalizarOpcional(cep);
        Bairro = NormalizarOpcional(bairro);
        Cidade = NormalizarOpcional(cidade);
        Estado = NormalizarOpcional(estado);
        SetTelefones(telefones);
        CorPrimaria = NormalizarOpcional(corPrimaria);
        Logo = NormalizarOpcional(logo);
    }

    /// <summary>Remove tudo que não for dígito, para comparar telefones de forma consistente.</summary>
    public static string NormalizarTelefone(string telefone) =>
        new(telefone.Where(char.IsDigit).ToArray());

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private void SetTelefones(IEnumerable<string>? telefones)
    {
        _telefones.Clear();

        if (telefones is null)
        {
            return;
        }

        foreach (var telefone in telefones)
        {
            var normalizado = NormalizarTelefone(telefone);
            if (normalizado.Length > 0)
            {
                _telefones.Add(normalizado);
            }
        }
    }
}