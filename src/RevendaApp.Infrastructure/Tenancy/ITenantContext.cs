namespace RevendaApp.Infrastructure.Tenancy;

/// <summary>
/// Contexto do tenant (empresa) do usuário autenticado, usado pelo <see cref="AppDbContext"/>
/// para aplicar o filtro global por empresa e pelo <see cref="SetEmpresaIdInterceptor"/> para
/// preencher o <c>EmpresaId</c> das linhas novas.
/// </summary>
/// <remarks>
/// A implementação concreta vive no projeto Web (<c>RevendaApp.Web.Common.Tenancy.TenantContext</c>),
/// porque precisa ler as claims do <c>AuthenticationStateProvider</c> (serviço do Blazor). Este
/// projeto (Infrastructure) só depende do contrato.
/// </remarks>
public interface ITenantContext
{
    /// <summary>Empresa (tenant) do usuário atual. <c>null</c> = central (super admin sem empresa).</summary>
    Guid? EmpresaId { get; }

    /// <summary>Nome da empresa para exibição no app bar.</summary>
    string NomeEmpresa { get; }

    /// <summary>Cor primária da empresa (hex, ex.: <c>#0A1F44</c>). Vazio = cor padrão.</summary>
    string CorPrimaria { get; }

    /// <summary>Indica se o usuário é o super administrador da central.</summary>
    bool EhSuperAdmin { get; }

    /// <summary>Indica se o super admin está "entrando como" alguma empresa.</summary>
    bool Impersonando { get; }

    /// <summary>
    /// Retorna o <see cref="EmpresaId"/> exigindo que exista; lança se a operação de domínio for
    /// executada sem empresa selecionada (ex.: central tentando gravar dado de domínio).
    /// </summary>
    /// <param name="acao">Descrição da ação, usada na mensagem de erro.</param>
    Guid EmpresaIdRequerida(string acao);
}