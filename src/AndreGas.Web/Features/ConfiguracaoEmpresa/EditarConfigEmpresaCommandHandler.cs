using AndreGas.Domain.Entities;
using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Tenancy;
using AndreGas.Web.Common;
using AndreGas.Web.Common.Authorization;
using AndreGas.Web.Common.Empresas;

using Mediator;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.ConfiguracaoEmpresa;

/// <summary>
/// Edita os dados de cadastro da <b>própria</b> empresa do usuário e grava um
/// <see cref="RegistroAuditoria"/> com o resumo "antes → depois".
/// </summary>
/// <remarks>
/// <para>
/// <b>Tenant:</b> a empresa vem de <see cref="ITenantContext.EmpresaIdRequerida"/> — é a primeira
/// instrução do handler. No modo central (super admin sem empresa) ela lança e nada é gravado: esta
/// tela não edita empresa por id.
/// </para>
/// <para>
/// <b>Autorização:</b> além da política de rota (<c>ApenasAdmin</c>), o handler chama
/// <see cref="IUsuarioAutenticado.RequerAdminAsync"/> — este é o limite real de segurança.
/// </para>
/// <para>
/// <b>Atomicidade da auditoria:</b> o registro é adicionado ao <b>mesmo</b>
/// <see cref="AppDbContext"/> e persistido no <b>mesmo</b> <c>SaveChangesAsync</c> da edição. Uma
/// edição que falhe (domínio ou banco) não grava nem a alteração nem a auditoria.
/// </para>
/// <para>
/// <b>Identificador imutável:</b> <see cref="Empresa.AtualizarDados"/> não o altera e o comando não
/// tem o campo. <b>Sem inativação:</b> o comando não expõe <c>Ativo</c>.
/// </para>
/// </remarks>
public sealed class EditarConfigEmpresaCommandHandler(
    AppDbContext db,
    ITenantContext tenant,
    IUsuarioAutenticado usuarioAutenticado,
    AuthenticationStateProvider? authStateProvider = null)
    : ICommandHandler<EditarConfigEmpresaCommand, bool>
{
    public async ValueTask<bool> Handle(EditarConfigEmpresaCommand command, CancellationToken cancellationToken)
    {
        // Primeira instrução: exige empresa selecionada (modo central não edita empresa).
        var empresaId = tenant.EmpresaIdRequerida("editar a empresa");

        // Limite real de segurança: a política da rota não basta.
        await usuarioAutenticado.RequerAdminAsync(cancellationToken);

        // `Empresas` não tem filtro global, mas a busca é pelo id do próprio tenant.
        var empresa = await db.Set<Empresa>()
            .FirstOrDefaultAsync(e => e.Id == empresaId, cancellationToken);

        if (empresa is null)
        {
            return false;
        }

        var antes = DescritorDeAlteracoesEmpresa.Retratar(empresa);

        // Identificador não é passado de propósito: é imutável após a criação.
        // A UF é normalizada AQUI (único ponto de escrita desta tela): o formulário e a API não
        // impõem caixa, e "sp" gravado como veio viraria uma UF diferente de "SP" em qualquer
        // comparação/relatório futuro. A coluna é `character varying(2)`; a regra do validador já
        // garante 2 letras ASCII, então `ToUpperInvariant` não muda o tamanho.
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

        var detalhes = DescritorDeAlteracoesEmpresa.Descrever(antes, DescritorDeAlteracoesEmpresa.Retratar(empresa));

        // Sem alteração não há evento (mesmo critério da central).
        if (detalhes is null)
        {
            return true;
        }

        var (usuarioNome, usuarioId) = await UsuarioAtual.ObterAsync(authStateProvider, db, cancellationToken);

        db.RegistrosAuditoria.Add(new RegistroAuditoria(
            empresaId,
            usuarioId,
            usuarioNome,
            RegistroAuditoria.Acoes.ConfigEmpresaEditada,
            detalhes,
            RegistroAuditoria.Origens.App));

        // Um único SaveChanges: edição + auditoria na mesma transação implícita.
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}