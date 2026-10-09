using Microsoft.AspNetCore.Identity;

namespace AndreGas.Infrastructure.Identity;

/// <summary>
/// Usuário autenticado do sistema. Além das propriedades do Identity (<see cref="IdentityUser{TKey}"/>),
/// guarda o <see cref="Nome"/> de exibição e pertence a um papel — <see cref="ApplicationRole.Admin"/> ou
/// <see cref="ApplicationRole.Vendedor"/>.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>Nome de exibição do usuário (ex.: na tela de Configurações e no menu).</summary>
    public string? Nome { get; set; }
}