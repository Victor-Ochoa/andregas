using Mediator;

namespace RevendaApp.Web.Features.Central.UsuariosEmpresa;

/// <summary>
/// Lista os usuários de uma empresa escolhida na central, com papel e status. O super admin usa
/// esta lista para criar/inativar usuários sem precisar "entrar como" a empresa.
/// </summary>
/// <param name="EmpresaId">Empresa cujos usuários serão listados.</param>
public sealed record ListarUsuariosEmpresaQuery(Guid EmpresaId) : IQuery<IReadOnlyList<UsuarioEmpresaItem>>;

/// <summary>Linha da listagem de usuários de uma empresa na central.</summary>
/// <param name="Id">Id do usuário.</param>
/// <param name="Nome">Nome de exibição.</param>
/// <param name="Email">E-mail (login).</param>
/// <param name="Papel">Role do Identity (<c>Admin</c>/<c>Vendedor</c>/<c>SuperAdmin</c>), se houver.</param>
/// <param name="Ativo">Indica se o usuário pode logar.</param>
public sealed record UsuarioEmpresaItem(Guid Id, string? Nome, string Email, string? Papel, bool Ativo);