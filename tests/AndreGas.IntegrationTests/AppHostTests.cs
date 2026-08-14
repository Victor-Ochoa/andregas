using Microsoft.Extensions.Logging;

namespace AndreGas.IntegrationTests.Tests;

/// <summary>
/// Testes de sanidade que sobem toda a distribuição via Aspire (incluindo o Postgres real em
/// container) para garantir que a orquestração e a aplicação web ficam saudáveis.
/// </summary>
public class AppHostTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task WebResourceStartsAndRespondsSuccessfully()
    {
        var cancellationToken = CancellationToken.None;

        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AndreGas_AppHost>(cancellationToken);
        appHost.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddFilter(appHost.Environment.ApplicationName, LogLevel.Debug);
            logging.AddFilter("Aspire.", LogLevel.Debug);
        });
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
            // The "web" project runs its https launch profile with the local ASP.NET Core dev
            // certificate, which isn't trusted in this test/CI environment — accept it here.
            clientBuilder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            });
        });

        await using var app = await appHost.BuildAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

        using var httpClient = app.CreateHttpClient("web", "http");
        await app.ResourceNotifications.WaitForResourceHealthyAsync("web", cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

        using var response = await httpClient.GetAsync("/", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
