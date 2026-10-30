using Mediator;

namespace AndreGas.Web.Features.Central.UsuariosEmpresa;

/// <summary>
/// Inativa ou reativa um usuário de uma empresa pela central.
/// </summary>
/// <param name="EmpresaId">Empresa do usuário (o usuário precisa pertencer a ela).</param>
/// <param name="UsuarioId">Usuário a (re)ativar.</param>
/// <param name="Ativo"><c>true</c> reativa; <c>false</c> inativa.</param>
public sealed record InativarUsuarioEmpresaCommand(Guid EmpresaId, Guid UsuarioId, bool Ativo) : ICommand<InativarUsuarioEmpresaResult>;

/// <summary>
/// Resultado da inativação/reativação. Recusas de negócio (usuário de outra empresa, último
/// administrador ativo) voltam como <c>Sucesso = false</c> com mensagem pt-BR, em vez de exceção.
/// </summary>
public sealed record InativarUsuarioEmpresaResult(bool Sucesso, string? Erro)
{
    public static InativarUsuarioEmpresaResult Ok() => new(true, null);

    public static InativarUsuarioEmpresaResult Falha(string erro) => new(false, erro);
}