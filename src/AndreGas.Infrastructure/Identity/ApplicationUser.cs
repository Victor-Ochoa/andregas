using Microsoft.AspNetCore.Identity;

namespace AndreGas.Infrastructure.Identity;

/// <summary>
/// Usuário autenticado do sistema. Por decisão de escopo, existe apenas um tipo de usuário
/// (sem papéis/permissões diferenciadas).
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
}
