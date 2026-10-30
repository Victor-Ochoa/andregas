using Mediator;

namespace RevendaApp.Web.Features.Central.UsuariosEmpresa;

/// <summary>
/// Cria um usuário (Admin ou Vendedor) numa empresa escolhida na central, sem o super admin
/// precisar "entrar como" a empresa.
/// </summary>
/// <param name="EmpresaId">Empresa que receberá o usuário.</param>
/// <param name="Nome">Nome de exibição.</param>
/// <param name="Email">E-mail (login).</param>
/// <param name="Papel">Role do Identity: <c>Admin</c> ou <c>Vendedor</c>.</param>
/// <param name="Senha">Senha inicial.</param>
public sealed record CriarUsuarioEmpresaCommand(
    Guid EmpresaId,
    string Nome,
    string Email,
    string Papel,
    string Senha) : ICommand<CriarUsuarioEmpresaResult>;

/// <summary>Resultado da criação de usuário na central.</summary>
public sealed record CriarUsuarioEmpresaResult(Guid? UsuarioId, string? Erro)
{
    public bool Sucesso => UsuarioId is not null;

    public static CriarUsuarioEmpresaResult Ok(Guid usuarioId) => new(usuarioId, null);

    public static CriarUsuarioEmpresaResult Falha(string erro) => new(null, erro);
}