using AndreGas.Web.Common.Authorization;

using Mediator;

namespace AndreGas.Web.Features.Usuarios.Cadastrar;

public sealed class CadastrarUsuarioCommandHandler(
    IUsuarioAutenticado usuarioAutenticado,
    IUsuarioWriter usuarioWriter)
    : ICommandHandler<CadastrarUsuarioCommand, CadastrarUsuarioResult>
{
    public async ValueTask<CadastrarUsuarioResult> Handle(CadastrarUsuarioCommand command, CancellationToken cancellationToken)
    {
        // Somente administradores podem cadastrar usuários. A UI desabilita o botão; o servidor
        // é o limite real.
        await usuarioAutenticado.RequerAdminAsync(cancellationToken);

        if (await usuarioWriter.EmailJaUsadoAsync(command.Email, cancellationToken))
        {
            return CadastrarUsuarioResult.Falha("Já existe um usuário cadastrado com esse e-mail.");
        }

        var (usuarioId, erro) = await usuarioWriter.CriarComRoleAsync(
            command.Nome, command.Email, command.Papel.ParaRole(), command.Senha, cancellationToken);

        return usuarioId is not null
            ? CadastrarUsuarioResult.Ok(usuarioId.Value)
            : CadastrarUsuarioResult.Falha(erro ?? "Não foi possível cadastrar o usuário.");
    }
}