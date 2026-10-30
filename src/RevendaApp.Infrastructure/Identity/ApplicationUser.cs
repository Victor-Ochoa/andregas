using Microsoft.AspNetCore.Identity;

namespace RevendaApp.Infrastructure.Identity;

/// <summary>
/// Usuário autenticado do sistema. Além das propriedades do Identity (<see cref="IdentityUser{TKey}"/>),
/// guarda o <see cref="Nome"/> de exibição e pertence a um papel — <see cref="ApplicationRole.Admin"/> ou
/// <see cref="ApplicationRole.Vendedor"/>.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>Nome de exibição do usuário (ex.: na tela de Configurações e no menu).</summary>
    public string? Nome { get; set; }

    /// <summary>
    /// Empresa (tenant) a que o usuário pertence. <c>null</c> = usuário da central (super admin,
    /// sem dados de domínio próprios).
    /// </summary>
    public Guid? EmpresaId { get; set; }

    /// <summary>Indica se o usuário está ativo (usuário inativo não loga).</summary>
    public bool Ativo { get; set; } = true;
}