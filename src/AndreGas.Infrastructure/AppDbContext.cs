using AndreGas.Domain.Entities;
using AndreGas.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Infrastructure;

/// <summary>
/// Contexto EF Core da aplicação: dados de domínio (clientes, produtos, vendas, ...) e
/// autenticação (ASP.NET Core Identity, com um único tipo de usuário, sem papéis).
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityUserContext<ApplicationUser, Guid>(options)
{
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<Venda> Vendas => Set<Venda>();
    public DbSet<ItemVenda> ItensVenda => Set<ItemVenda>();
    public DbSet<Pagamento> Pagamentos => Set<Pagamento>();
    public DbSet<MovimentacaoEstoque> MovimentacoesEstoque => Set<MovimentacaoEstoque>();
    public DbSet<HistoricoEstoque> HistoricosEstoque => Set<HistoricoEstoque>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
