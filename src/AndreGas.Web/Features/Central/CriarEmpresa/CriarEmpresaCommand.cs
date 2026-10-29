using Mediator;

namespace AndreGas.Web.Features.Central.CriarEmpresa;

/// <summary>
/// Cadastra uma empresa (tenant) e, junto, o seu <b>primeiro administrador</b> — as duas coisas
/// numa única transação (ver <see cref="CriarEmpresaCommandHandler"/>). Só o super admin da central
/// executa.
/// </summary>
/// <param name="Nome">Nome exibido da empresa (obrigatório).</param>
/// <param name="Identificador">Slug único e imutável (ex.: <c>andre-gas</c>): apenas <c>[a-z0-9-]</c>.</param>
/// <param name="Email">E-mail do primeiro administrador (obrigatório).</param>
/// <param name="Senha">Senha do primeiro administrador (obrigatório).</param>
/// <param name="NomeAdmin">Nome de exibição do primeiro administrador (obrigatório).</param>
/// <param name="Cnpj">CNPJ (opcional).</param>
/// <param name="Endereco">Logradouro (opcional).</param>
/// <param name="Numero">Número (opcional).</param>
/// <param name="Cep">CEP (opcional).</param>
/// <param name="Bairro">Bairro (opcional).</param>
/// <param name="Cidade">Cidade (opcional).</param>
/// <param name="Estado">UF (opcional).</param>
/// <param name="Telefones">Telefones de contato (opcional).</param>
/// <param name="CorPrimaria">Cor primária em hex (opcional).</param>
/// <param name="Logo">Logo como data-uri base64 (opcional, ~500KB).</param>
public sealed record CriarEmpresaCommand(
    string Nome,
    string Identificador,
    string Email,
    string Senha,
    string NomeAdmin,
    string? Cnpj = null,
    string? Endereco = null,
    string? Numero = null,
    string? Cep = null,
    string? Bairro = null,
    string? Cidade = null,
    string? Estado = null,
    IReadOnlyList<string>? Telefones = null,
    string? CorPrimaria = null,
    string? Logo = null) : ICommand<CriarEmpresaResult>;

/// <summary>
/// Resultado da criação de empresa: ids da empresa e do administrador criados, ou a mensagem de
/// erro (pt-BR) quando a operação é recusada. Em caso de falha <b>nada</b> é persistido.
/// </summary>
public sealed record CriarEmpresaResult(Guid? EmpresaId, Guid? AdminId, string? Erro)
{
    public bool Sucesso => EmpresaId is not null && AdminId is not null;

    public static CriarEmpresaResult Ok(Guid empresaId, Guid adminId) => new(empresaId, adminId, null);

    public static CriarEmpresaResult Falha(string erro) => new(null, null, erro);
}