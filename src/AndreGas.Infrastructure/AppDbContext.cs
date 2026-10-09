using AndreGas.Domain.Entities;
using AndreGas.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Infrastructure;

/// <summary>
/// Contexto EF Core da aplicação: dados de domínio (clientes, produtos, vendas, ...) e
/// autenticação (ASP.NET Core Identity, com usuários pertencendo a um papel — Admin ou Vendedor).
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<Venda> Vendas => Set<Venda>();
    public DbSet<ItemVenda> ItensVenda => Set<ItemVenda>();
    public DbSet<Pagamento> Pagamentos => Set<Pagamento>();
    public DbSet<MovimentacaoEstoque> MovimentacoesEstoque => Set<MovimentacaoEstoque>();
    public DbSet<HistoricoEstoque> HistoricosEstoque => Set<HistoricoEstoque>();
    public DbSet<PrecoPersonalizadoCliente> PrecosPersonalizadosClientes => Set<PrecoPersonalizadoCliente>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}