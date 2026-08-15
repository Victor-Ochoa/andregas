using Aspire.Hosting;
using Microsoft.Extensions.Logging;

namespace AndreGas.IntegrationTests.Tests;

/// <summary>
/// Sobe a distribuição Aspire completa (Postgres real + app web) uma única vez e a reutiliza
/// entre os testes desta coleção, para evitar pagar o custo de subir o container a cada teste.
/// </summary>
public sealed class WebAppFixture : IAsyncLifetime
{
    private DistributedApplication _app = null!;

    public Uri WebBaseAddress { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var cancellationToken = CancellationToken.None;

        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AndreGas_AppHost>(cancellationToken);
        appHost.Services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });

        _app = await appHost.BuildAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);
        await _app.StartAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("web", cancellationToken).WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);

        WebBaseAddress = _app.GetEndpoint("web", "https");
    }

    public async Task DisposeAsync()
    {
        await _app.DisposeAsync();
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
