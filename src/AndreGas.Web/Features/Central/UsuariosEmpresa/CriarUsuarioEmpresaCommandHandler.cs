using AndreGas.Domain.Entities;
using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Tenancy;
using AndreGas.Web.Features.Usuarios.Cadastrar;

using Mediator;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Central.UsuariosEmpresa;

/// <summary>
/// Cria um usuário vinculado a uma empresa escolhida na central. O <c>EmpresaId</c> é atribuído
/// explicitamente (o Identity não é entidade de domínio, o interceptor de tenancy não o cobre) e a
/// empresa precisa existir.
/// </summary>
public sealed class CriarUsuarioEmpresaCommandHandler(AppDbContext db, ITenantContext tenant, IUsuarioWriter usuarioWriter)
    : ICommandHandler<CriarUsuarioEmpresaCommand, CriarUsuarioEmpresaResult>
{
    public async ValueTask<CriarUsuarioEmpresaResult> Handle(CriarUsuarioEmpresaCommand command, CancellationToken cancellationToken)
    {
        // Limite real de segurança: a política da rota não basta, o handler reforça no servidor.
        if (!tenant.EhSuperAdmin)
        {
            throw new InvalidOperationException("Somente o super administrador da central pode criar usuários em uma empresa.");
        }

        // A empresa alvo precisa existir: sem essa checagem, a FK Restrict estouraria com um erro
        // técnico em vez de uma mensagem de negócio.
        var empresaExiste = await db.Set<Empresa>().AnyAsync(e => e.Id == command.EmpresaId, cancellationToken);
        if (!empresaExiste)
        {
            return CriarUsuarioEmpresaResult.Falha("Empresa não encontrada.");
        }

        if (await usuarioWriter.EmailJaUsadoAsync(command.Email, cancellationToken))
        {
            return CriarUsuarioEmpresaResult.Falha("Já existe um usuário cadastrado com esse e-mail.");
        }

        var (usuarioId, erro) = await usuarioWriter.CriarComRoleAsync(
            command.Nome, command.Email, command.Papel, command.Senha, command.EmpresaId, cancellationToken);

        return usuarioId is not null
            ? CriarUsuarioEmpresaResult.Ok(usuarioId.Value)
            : CriarUsuarioEmpresaResult.Falha(erro ?? "Não foi possível criar o usuário.");
    }
}