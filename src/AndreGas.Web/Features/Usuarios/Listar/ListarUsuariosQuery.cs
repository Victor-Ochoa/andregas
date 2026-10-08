using Mediator;

namespace AndreGas.Web.Features.Usuarios.Listar;

/// <summary>Lista os usuários do sistema com seu respectivo papel, para exibição na tela de
/// Configurações. Restrito a administradores (ver política da página).</summary>
public sealed record ListarUsuariosQuery : IQuery<IReadOnlyList<UsuarioListItem>>;

public sealed record UsuarioListItem(
    Guid Id,
    string? Nome,
    string Email,
    string? Papel);