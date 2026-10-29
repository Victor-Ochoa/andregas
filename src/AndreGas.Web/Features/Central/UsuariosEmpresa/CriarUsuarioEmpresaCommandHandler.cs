using AndreGas.Domain.Entities;
using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Identity;
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
/// <remarks>
/// <b>Papel:</b> a lista permitida é checada <b>aqui</b>, não só no validador. O validador roda no
/// pipeline do Mediator e é a mensagem amigável da UI, mas o handler é o limite real: aceitar
/// <c>SuperAdmin</c> criaria um usuário com <c>EmpresaId</c> <b>e</b> a role de super admin — o
/// principal resultante teria <c>EhSuperAdmin=true</c> e <c>ClaimEmpresaId</c>, passando tanto em
/// <c>ApenasSuperAdmin</c> quanto em todos os guardas <c>tenant.EhSuperAdmin</c>.
/// </remarks>
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

        // Lista fechada de papéis: recusa (fail loud) em vez de confiar no validador.
        if (!CriarUsuarioEmpresaCommandValidator.PapeisPermitidos.Contains(command.Papel))
        {
            throw new InvalidOperationException(
                $"O papel '{command.Papel}' não pode ser atribuído pela central. Use Administrador ou Vendedor.");
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