using AndreGas.Infrastructure.Identity;

namespace AndreGas.Web.Features.Usuarios;

/// <summary>
/// Papéis disponíveis ao criar/editar um usuário na tela de Configurações. Valores de exibição
/// em pt-BR; a role do Identity é a string correspondente em <see cref="ApplicationRole"/>.
/// </summary>
public enum UsuarioPapel
{
    Admin,
    Vendedor,
}

public static class UsuarioPapelExtensions
{
    public static string ParaRole(this UsuarioPapel papel) => papel switch
    {
        UsuarioPapel.Admin => ApplicationRole.Admin,
        UsuarioPapel.Vendedor => ApplicationRole.Vendedor,
        _ => throw new ArgumentOutOfRangeException(nameof(papel), papel, null),
    };

    public static string Rotulo(this UsuarioPapel papel) => papel switch
    {
        UsuarioPapel.Admin => "Administrador",
        UsuarioPapel.Vendedor => "Vendedor",
        _ => papel.ToString(),
    };
}