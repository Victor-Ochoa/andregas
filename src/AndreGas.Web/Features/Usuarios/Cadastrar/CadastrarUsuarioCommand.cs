using Mediator;

namespace AndreGas.Web.Features.Usuarios.Cadastrar;

/// <summary>Cadastra um novo usuário do sistema (Admin ou Vendedor) com senha definida pelo admin.</summary>
public sealed record CadastrarUsuarioCommand(
    string Nome,
    string Email,
    UsuarioPapel Papel,
    string Senha,
    string ConfirmarSenha) : ICommand<CadastrarUsuarioResult>;

public sealed record CadastrarUsuarioResult(Guid? UsuarioId, string? Erro)
{
    public bool Sucesso => UsuarioId is not null;

    public static CadastrarUsuarioResult Ok(Guid usuarioId) => new(usuarioId, null);

    public static CadastrarUsuarioResult Falha(string erro) => new(null, erro);
}