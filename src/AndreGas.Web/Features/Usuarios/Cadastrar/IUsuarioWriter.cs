namespace AndreGas.Web.Features.Usuarios.Cadastrar;

/// <summary>
/// Abstração fina sobre o <c>UserManager</c> do Identity para criação de usuário e atribuição de
/// papel, mantendo o <see cref="CadastrarUsuarioCommandHandler"/> testável sem depender de uma
/// classe com muitas dependências (mesmo princípio do <c>IPasswordSignIn</c> do login).
/// </summary>
public interface IUsuarioWriter
{
    /// <summary>Verifica se já existe um usuário com o e-mail informado (case-insensitive).</summary>
    Task<bool> EmailJaUsadoAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria o usuário com a senha fornecida e o vincula à role indicada. Retorna o Id do usuário
    /// criado, ou null com uma descrição de erro em caso de falha do Identity.
    /// </summary>
    Task<(Guid? UsuarioId, string? Erro)> CriarComRoleAsync(
        string nome, string email, string papel, string senha, CancellationToken cancellationToken = default);
}