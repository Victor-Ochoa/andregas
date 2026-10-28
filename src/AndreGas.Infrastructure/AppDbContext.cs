using AndreGas.Domain.Entities;
using AndreGas.Infrastructure.Identity;
using AndreGas.Infrastructure.Tenancy;

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Infrastructure;

/// <summary>
/// Contexto EF Core da aplicação: dados de domínio (clientes, produtos, vendas, ...) e
/// autenticação (ASP.NET Core Identity, com usuários pertencendo a um papel — Admin ou Vendedor).
/// </summary>
/// <remarks>
/// <para>
/// Multi-empresa (multi-tenant): todas as tabelas de domínio têm <c>EmpresaId</c> e um filtro
/// global que isola os dados da empresa do usuário. O tenant vem do <see cref="ITenantContext"/>
/// opcional injetado no construtor; <c>null</c> = modo central (super admin, seed, testes e
/// reset), que enxerga todas as empresas.
/// </para>
/// <para>
/// O filtro usa o campo <see cref="_empresaId"/> (closure), avaliado por query — a expressão é
/// idêntica para todas as instâncias, então um único model cacheado serve a todos os tenants.
/// </para>
/// <para>
/// <b>Importante:</b> para o tenant ser o da requisição/circuito corrente, o <c>AppDbContext</c>
/// precisa ser registrado como <b>scoped</b> (não pooled). Ver o comentário em <c>Program.cs</c>
/// sobre por que usamos <c>AddDbContext</c> + <c>EnrichNpgsqlDbContext</c> em vez de
/// <c>AddNpgsqlDbContext</c> (que faz pooling e capturaria um tenant fixo da raiz).
/// </para>
/// </remarks>
public class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext? tenant = null) : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    /// <summary>
    /// Empresa do tenant atual, capturada uma vez na construção do contexto (o tenant é imutável
    /// durante a vida do contexto — um contexto por escopo/circuito). <c>null</c> = modo central
    /// (vê tudo). O filtro global fecha sobre este campo readonly; a expressão é idêntica para
    /// todas as instâncias, então um único model cacheado serve a todos os tenants.
    /// </summary>
    private readonly Guid? _empresaId = tenant?.EmpresaId;

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<Venda> Vendas => Set<Venda>();
    public DbSet<ItemVenda> ItensVenda => Set<ItemVenda>();
    public DbSet<Pagamento> Pagamentos => Set<Pagamento>();
    public DbSet<MovimentacaoEstoque> MovimentacoesEstoque => Set<MovimentacaoEstoque>();
    public DbSet<HistoricoEstoque> HistoricosEstoque => Set<HistoricoEstoque>();
    public DbSet<PrecoPersonalizadoCliente> PrecosPersonalizadosClientes => Set<PrecoPersonalizadoCliente>();
    public DbSet<EmprestimoVasilhame> EmprestimosVasilhames => Set<EmprestimoVasilhame>();
    public DbSet<RecorrenciaCompra> RecorrenciasCompra => Set<RecorrenciaCompra>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Isolamento por empresa: cada tabela de domínio só enxerga linhas da empresa do tenant.
        // `_empresaId == null` = modo central → vê todas as empresas.
        // `Empresas` e as tabelas do Identity NÃO têm filtro.
        builder.Entity<Cliente>().HasQueryFilter(c => _empresaId == null || c.EmpresaId == _empresaId);
        builder.Entity<Produto>().HasQueryFilter(p => _empresaId == null || p.EmpresaId == _empresaId);
        builder.Entity<Venda>().HasQueryFilter(v => _empresaId == null || v.EmpresaId == _empresaId);
        builder.Entity<ItemVenda>().HasQueryFilter(i => _empresaId == null || i.EmpresaId == _empresaId);
        builder.Entity<Pagamento>().HasQueryFilter(p => _empresaId == null || p.EmpresaId == _empresaId);
        builder.Entity<MovimentacaoEstoque>().HasQueryFilter(m => _empresaId == null || m.EmpresaId == _empresaId);
        builder.Entity<HistoricoEstoque>().HasQueryFilter(h => _empresaId == null || h.EmpresaId == _empresaId);
        builder.Entity<PrecoPersonalizadoCliente>().HasQueryFilter(p => _empresaId == null || p.EmpresaId == _empresaId);
        builder.Entity<EmprestimoVasilhame>().HasQueryFilter(e => _empresaId == null || e.EmpresaId == _empresaId);
        builder.Entity<RecorrenciaCompra>().HasQueryFilter(r => _empresaId == null || r.EmpresaId == _empresaId);
    }
}