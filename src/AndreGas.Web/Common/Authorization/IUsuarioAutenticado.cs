namespace AndreGas.Web.Common.Authorization;

/// <summary>
/// Abstração para checar o papel (role) do usuário autenticado atual no circuito Blazor.
/// É a fonte de verdade de autorização nos handlers e componentes (não apenas ocultação de UI):
/// <see cref="RequerAdminAsync"/> lança quando o usuário não é administrador, impedindo a operação
/// no servidor.
/// </summary>
public interface IUsuarioAutenticado
{
    /// <summary>Retorna true se o usuário autenticado atual pertence à role Admin.</summary>
    Task<bool> EhAdministradorAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Garante que o usuário atual seja administrador; caso contrário, lança
    /// <see cref="InvalidOperationException"/> (a operação é rejeitada no servidor).
    /// </summary>
    Task RequerAdminAsync(CancellationToken cancellationToken = default);
}