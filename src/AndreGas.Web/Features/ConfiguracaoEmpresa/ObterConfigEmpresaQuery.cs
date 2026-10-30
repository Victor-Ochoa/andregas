using AndreGas.Domain.Entities;
using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Tenancy;

using Mediator;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.ConfiguracaoEmpresa;

/// <summary>
/// Carrega os dados de cadastro da <b>própria</b> empresa do usuário para preencher a aba
/// "Minha Empresa". A empresa vem do tenant (não há id no parâmetro).
/// </summary>
public sealed record ObterConfigEmpresaQuery : IQuery<ConfigEmpresaDetalhe?>;

/// <summary>
/// Dados da empresa para o formulário. <see cref="Identificador"/> vem apenas para exibição
/// (somente leitura) — não é editável.
/// </summary>
public sealed record ConfigEmpresaDetalhe(
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
    string? Logo);

/// <summary>
/// Handler do detalhe da empresa do tenant. Começa por
/// <see cref="ITenantContext.EmpresaIdRequerida"/>: no modo central (super admin sem empresa) lança
/// em vez de devolver dados de alguma empresa arbitrária.
/// </summary>
public sealed class ObterConfigEmpresaQueryHandler(AppDbContext db, ITenantContext tenant)
    : IQueryHandler<ObterConfigEmpresaQuery, ConfigEmpresaDetalhe?>
{
    public async ValueTask<ConfigEmpresaDetalhe?> Handle(
        ObterConfigEmpresaQuery query, CancellationToken cancellationToken)
    {
        var empresaId = tenant.EmpresaIdRequerida("consultar os dados da empresa");

        var empresa = await db.Set<Empresa>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == empresaId, cancellationToken);

        return empresa is null
            ? null
            : new ConfigEmpresaDetalhe(
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
                empresa.Logo);
    }
}