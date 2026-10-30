using AndreGas.Domain.Entities;
using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Tenancy;
using AndreGas.Web.Common;
using AndreGas.Web.Common.Empresas;

using Mediator;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Central.EditarEmpresa;

/// <summary>
/// Edita os dados de cadastro de uma empresa. Retorna <c>false</c> quando a empresa não existe.
/// </summary>
/// <remarks>
/// <para>
/// A tabela <c>Empresas</c> não tem filtro global, então a busca funciona igual nos modos central
/// e de empresa; ainda assim o handler exige super admin — é o limite real de segurança.
/// </para>
/// <para>
/// <b>Auditoria:</b> grava um <see cref="RegistroAuditoria"/> com o resumo "antes → depois" no
/// <b>mesmo</b> <c>SaveChanges</c> da edição (mesmo formato da aba "Minha Empresa", via
/// <see cref="DescritorDeAlteracoesEmpresa"/>). Uma edição que falhe não grava nem a alteração nem
/// o registro.
/// </para>
/// </remarks>
public sealed class EditarEmpresaCommandHandler(
    AppDbContext db,
    ITenantContext tenant,
    AuthenticationStateProvider? authStateProvider = null)
    : ICommandHandler<EditarEmpresaCommand, bool>
{
    public async ValueTask<bool> Handle(EditarEmpresaCommand command, CancellationToken cancellationToken)
    {
        // Limite real de segurança: a política da rota não basta, o handler reforça no servidor.
        if (!tenant.EhSuperAdmin)
        {
            throw new InvalidOperationException("Somente o super administrador da central pode editar empresas.");
        }

        var empresa = await db.Set<Empresa>().FirstOrDefaultAsync(e => e.Id == command.EmpresaId, cancellationToken);
        if (empresa is null)
        {
            return false;
        }

        var antes = DescritorDeAlteracoesEmpresa.Retratar(empresa);

        // Identificador não é passado de propósito: é imutável após a criação.
        empresa.AtualizarDados(
            command.Nome,
            command.Cnpj,
            command.Endereco,
            command.Numero,
            command.Cep,
            command.Bairro,
            command.Cidade,
            command.Estado,
            command.Telefones,
            command.CorPrimaria,
            command.Logo);

        var detalhes = DescritorDeAlteracoesEmpresa.Descrever(
            antes, DescritorDeAlteracoesEmpresa.Retratar(empresa));

        // Sem alteração não há evento: gravar "Nenhuma alteração…" polui a trilha com um salvamento
        // que não mudou nada e esconde os que mudaram.
        if (detalhes is null)
        {
            return true;
        }

        var (usuarioNome, usuarioId) = await UsuarioAtual.ObterAsync(authStateProvider, db, cancellationToken);

        db.RegistrosAuditoria.Add(new RegistroAuditoria(
            empresa.Id,
            usuarioId,
            usuarioNome,
            RegistroAuditoria.Acoes.EmpresaEditada,
            detalhes,
            RegistroAuditoria.Origens.Central));

        // Um único SaveChanges: edição + auditoria na mesma transação implícita.
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}