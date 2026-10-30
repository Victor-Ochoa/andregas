using Mediator;

using Microsoft.EntityFrameworkCore;

using RevendaApp.Domain.Entities;
using RevendaApp.Infrastructure;
using RevendaApp.Infrastructure.Tenancy;

namespace RevendaApp.Web.Features.Central.EditarEmpresa;

/// <summary>
/// Carrega os dados completos de uma empresa para o formulário de edição da central. Complementa
/// <see cref="RevendaApp.Web.Features.Central.ListarEmpresas.EmpresaResumo"/>, que não traz
/// telefones/logo/endereço completo (e não deve trafegar o base64 do logo para a grade).
/// </summary>
/// <param name="EmpresaId">Empresa a carregar.</param>
public sealed record ObterEmpresaQuery(Guid EmpresaId) : IQuery<EmpresaDetalhe?>;

/// <summary>Dados de cadastro de uma empresa para edição (o identificador é somente leitura).</summary>
public sealed record EmpresaDetalhe(
    Guid Id,
    string Identificador,
    string Nome,
    string? Cnpj,
    string? Endereco,
    string? Numero,
    string? Cep,
    string? Bairro,
    string? Cidade,
    string? Estado,
    IReadOnlyList<string> Telefones,
    string? CorPrimaria,
    string? Logo,
    bool Ativo);

/// <summary>
/// Handler do detalhe de empresa. A tabela <c>Empresas</c> não tem filtro global, mas o handler
/// ainda exige super admin (limite real de segurança).
/// </summary>
public sealed class ObterEmpresaQueryHandler(AppDbContext db, ITenantContext tenant)
    : IQueryHandler<ObterEmpresaQuery, EmpresaDetalhe?>
{
    public async ValueTask<EmpresaDetalhe?> Handle(ObterEmpresaQuery query, CancellationToken cancellationToken)
    {
        if (!tenant.EhSuperAdmin)
        {
            throw new InvalidOperationException("Somente o super administrador da central pode consultar os dados de uma empresa.");
        }

        var empresa = await db.Set<Empresa>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == query.EmpresaId, cancellationToken);

        return empresa is null
            ? null
            : new EmpresaDetalhe(
                empresa.Id,
                empresa.Identificador,
                empresa.Nome,
                empresa.Cnpj,
                empresa.Endereco,
                empresa.Numero,
                empresa.Cep,
                empresa.Bairro,
                empresa.Cidade,
                empresa.Estado,
                empresa.Telefones.ToList(),
                empresa.CorPrimaria,
                empresa.Logo,
                empresa.Ativo);
    }
}