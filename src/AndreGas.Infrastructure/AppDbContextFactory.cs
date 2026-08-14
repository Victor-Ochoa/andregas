using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AndreGas.Infrastructure;

/// <summary>
/// Fábrica usada apenas em tempo de design pelas ferramentas do EF Core (dotnet ef migrations),
/// já que em tempo de execução a connection string vem do Aspire (Aspire.Npgsql...).
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=andregas_design;Username=postgres;Password=postgres");
        return new AppDbContext(optionsBuilder.Options);
    }
}
