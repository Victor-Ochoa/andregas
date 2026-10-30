using Microsoft.EntityFrameworkCore;

using RevendaApp.Domain.Entities;
using RevendaApp.Infrastructure;
using RevendaApp.Infrastructure.Tenancy;
using RevendaApp.Web.Theme;

namespace RevendaApp.Web.Common;

/// <summary>
/// Identidade exibida na interface: marca do produto (<c>Revenda.App</c>) e, quando há uma empresa
/// selecionada, nome/cor/logo dela.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="NomeEmpresa"/> e <see cref="CorPrimaria"/> vêm das claims (via
/// <see cref="ITenantContext"/>), sem I/O. O logo <b>não</b> vai para as claims — um base64 pode
/// ter ~500KB e estouraria o cookie de autenticação — então é lido de <c>Empresa.Logo</c> no banco
/// e memoizado por escopo.
/// </para>
/// <para>
/// O modo central (super admin sem empresa) é legítimo aqui: nunca chamamos
/// <see cref="ITenantContext.EmpresaIdRequerida"/>; sem empresa, a identidade é a do produto.
/// </para>
/// </remarks>
public sealed class IdentidadeEmpresaService(ITenantContext tenant, AppDbContext db)
{
    /// <summary>Nome do produto, exibido quando não há empresa (login e modo central).</summary>
    public const string NomeProduto = "Revenda.App";

    private string? _logo;
    private bool _logoCarregado;

    /// <summary>Nome exibido: o da empresa, ou <see cref="NomeProduto"/> no modo central.</summary>
    public string NomeEmpresa =>
        string.IsNullOrWhiteSpace(tenant.NomeEmpresa) ? NomeProduto : tenant.NomeEmpresa.Trim();

    /// <summary>Cor primária da empresa (hex); cor padrão do tema quando ela não define uma.</summary>
    public string CorPrimaria =>
        string.IsNullOrWhiteSpace(tenant.CorPrimaria) ? RevendaAppTheme.CorPrimariaPadrao : tenant.CorPrimaria.Trim();

    /// <summary>Indica se há uma empresa selecionada (falso = central/super admin).</summary>
    public bool TemEmpresa => tenant.EmpresaId is not null;

    /// <summary>
    /// Logo da empresa (data-uri base64) ou <c>null</c>. Lido do banco na primeira leitura e
    /// mantido no escopo — o app bar é renderizado em toda navegação.
    /// </summary>
    public string? Logo
    {
        get
        {
            if (!_logoCarregado)
            {
                _logo = CarregarLogo();
                _logoCarregado = true;
            }

            return _logo;
        }
    }

    /// <summary>Título da aba: <c>"{tela} — {NomeEmpresa}"</c>.</summary>
    /// <param name="tela">Nome da tela (ex.: "Estoque").</param>
    public string Titulo(string tela) => $"{tela} — {NomeEmpresa}";

    private string? CarregarLogo()
    {
        if (tenant.EmpresaId is not { } empresaId)
        {
            return null;
        }

        // `Empresas` não tem filtro global por empresa, então a consulta vale também no modo
        // central — mas só consultamos quando há empresa selecionada.
        return db.Set<Empresa>()
            .AsNoTracking()
            .Where(e => e.Id == empresaId)
            .Select(e => e.Logo)
            .FirstOrDefault();
    }
}