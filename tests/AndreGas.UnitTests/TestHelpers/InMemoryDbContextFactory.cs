using AndreGas.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.UnitTests.TestHelpers;

/// <summary>Cria instâncias de <see cref="AppDbContext"/> com o provider InMemory do EF Core,
/// para testar validators/handlers sem precisar de um Postgres real.</summary>
public static class InMemoryDbContextFactory
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}