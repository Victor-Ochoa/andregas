using AndreGas.Infrastructure.Tenancy;
using AndreGas.Web.Common.Authorization;

using Mediator;

namespace AndreGas.Web.Features.Usuarios.Cadastrar;

public sealed class CadastrarUsuarioCommandHandler(
    IUsuarioAutenticado usuarioAutenticado,
    IUsuarioWriter usuarioWriter,
    ITenantContext tenant)
    : ICommandHandler<CadastrarUsuarioCommand, CadastrarUsuarioResult>
{
    public async ValueTask<CadastrarUsuarioResult> Handle(CadastrarUsuarioCommand command, CancellationToken cancellationToken)
    {
        // Somente administradores podem cadastrar usuários. A UI desabilita o botão; o servidor
        // é o limite real.
        await usuarioAutenticado.RequerAdminAsync(cancellationToken);

        // O usuário criado pertence à empresa do tenant corrente. O caminho do super admin da
        // central criar usuários para uma empresa escolhida ainda não existe (Task 10) — até lá,
        // operar sem empresa selecionada falha em vez de criar usuário órfão.
        var empresaId = tenant.EmpresaIdRequerida("cadastrar usuário");

        if (await usuarioWriter.EmailJaUsadoAsync(command.Email, cancellationToken))
        {
            return CadastrarUsuarioResult.Falha("Já existe um usuário cadastrado com esse e-mail.");
        }

        var (usuarioId, erro) = await usuarioWriter.CriarComRoleAsync(
            command.Nome, command.Email, command.Papel.ParaRole(), command.Senha, empresaId, cancellationToken);

        return usuarioId is not null
            ? CadastrarUsuarioResult.Ok(usuarioId.Value)
            : CadastrarUsuarioResult.Falha(erro ?? "Não foi possível cadastrar o usuário.");
    }
}