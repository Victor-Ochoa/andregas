using AndreGas.Infrastructure;

using Aspire.Hosting;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AndreGas.IntegrationTests.Tests;

/// <summary>
/// Sobe a distribuição Aspire completa (Postgres real + app web) uma única vez e a reutiliza
/// entre os testes desta coleção, para evitar pagar o custo de subir o container a cada teste.
/// </summary>
public sealed class WebAppFixture : IAsyncLifetime
{
    private DistributedApplication _app = null!;
    private string _connectionString = null!;

    public Uri WebBaseAddress { get; private set; } = null!;

    /// <summary>Connection string do Postgres real gerenciado pelo Aspire.</summary>
    public string ConnectionString => _connectionString;

    public async Task InitializeAsync()
    {
        var cancellationToken = CancellationToken.None;

        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AndreGas_AppHost>(cancellationToken);

        // O AppHost registra seed-admin-email/seed-admin-password como Parameters SEM valor padrão
        // (o valor vem de config em produção). Em testes via Aspire.Hosting.Testing essa config não
        // existe, e o recurso "web" falha a inicialização com MissingParameterValueException.
        // Provemos valores válidos aqui para o seed do admin funcionar nos testes.
        appHost.Configuration["Parameters:seed-admin-email"] = "admin@andregas.com.br";
        appHost.Configuration["Parameters:seed-admin-password"] = "AndreGas@123";

        appHost.Services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });

        _app = await appHost.BuildAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(150), cancellationToken);
        await _app.StartAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(150), cancellationToken);
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("web", cancellationToken).WaitAsync(TimeSpan.FromSeconds(150), cancellationToken);

        WebBaseAddress = _app.GetEndpoint("web", "https");
        _connectionString = await _app.GetConnectionStringAsync("andregas", cancellationToken)
            ?? throw new InvalidOperationException("Não foi possível resolver a connection string do recurso 'andregas'.");
    }

    public async Task DisposeAsync()
    {
        await _app.DisposeAsync();
    }

    /// <summary>Cria um <see cref="AppDbContext"/> apontando para o Postgres real gerenciado pelo
    /// Aspire (usado para preparar/se mitigar dados — ex.: criar um usuário vendedor para testes
    /// de autorização E2E).</summary>
    public AndreGas.Infrastructure.AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AndreGas.Infrastructure.AppDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        var context = new AndreGas.Infrastructure.AppDbContext(options);
        context.Database.Migrate();
        return context;
    }

    /// <summary>Cria um HttpClient com cookies próprios (isolado de outros testes) apontando
    /// para a aplicação web em execução.</summary>
    public HttpClient CreateClient() => new(new HttpClientHandler
    {
        UseCookies = true,
        CookieContainer = new System.Net.CookieContainer(),
        AllowAutoRedirect = false,
        // The "web" project uses the local ASP.NET Core dev certificate, which isn't trusted
        // in this test/CI environment — accept it here.
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
    })
    {
        BaseAddress = WebBaseAddress,
    };
}