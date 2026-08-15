using Aspire.Hosting;
using AndreGas.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AndreGas.IntegrationTests.Tests;

/// <summary>
/// Sobe apenas o Postgres real (via o mesmo AppHost usado em produção) para testar handlers que
/// usam <see cref="AppDbContext"/> diretamente, sem o overhead de subir o processo web inteiro.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private DistributedApplication _app = null!;
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        var cancellationToken = CancellationToken.None;

        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AndreGas_AppHost>(cancellationToken);
        appHost.Services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));

        _app = await appHost.BuildAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(150), cancellationToken);
        await _app.StartAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(150), cancellationToken);
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("andregas", cancellationToken).WaitAsync(TimeSpan.FromSeconds(150), cancellationToken);

        _connectionString = await _app.GetConnectionStringAsync("andregas", cancellationToken)
            ?? throw new InvalidOperationException("Não foi possível resolver a connection string do recurso 'andregas'.");
    }

    public async Task DisposeAsync()
    {
        await _app.DisposeAsync();
    }

    /// <summary>Cria um <see cref="AppDbContext"/> apontando para o Postgres real gerenciado
    /// pelo Aspire, garantindo que as migrações estejam aplicadas.</summary>
    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        var context = new AppDbContext(options);
        context.Database.Migrate();
        return context;
    }
}
