using Microsoft.AspNetCore.Identity;

namespace AndreGas.Infrastructure.Identity;

/// <summary>
/// Papéis de usuário do sistema. O administrador (admin do seed ou criado por outro admin pela
/// tela de Configurações) tem acesso à configuração e às mutações de estoque; o vendedor tem acesso
/// operacional (dashboards, vendas, clientes, fiados) sem privilégios de administração.
/// </summary>
public sealed class ApplicationRole : IdentityRole<Guid>
{
    public const string Admin = "Admin";
    public const string Vendedor = "Vendedor";

    public ApplicationRole() { }

    public ApplicationRole(string roleName) : base(roleName)
    {
        Id = Guid.NewGuid();
    }
}