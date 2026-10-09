using System.Text.RegularExpressions;

namespace AndreGas.IntegrationTests.Tests;

/// <summary>
/// Testes de integração ponta a ponta do fluxo de login: sobem a aplicação real com Postgres
/// (via <see cref="WebAppFixture"/>), simulam o form POST estático (SSR) que o Blazor gera,
/// e verificam o cookie de autenticação do Identity.
/// </summary>
public class LoginFlowTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    private const string AdminEmail = "admin@andregas.com.br";
    private const string AdminPassword = "AndreGas@123";

    [Fact]
    public async Task UnauthenticatedRequest_IsRedirectedToLogin()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_WithValidCredentials_SetsAuthCookieAndAllowsAccessToHome()
    {
        using var client = fixture.CreateClient();

        var (token, handler) = await GetAntiforgeryFieldsAsync(client, "/login");

        using var loginResponse = await PostLoginAsync(client, token, handler, AdminEmail, AdminPassword);

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        using var homeResponse = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, homeResponse.StatusCode);

        var homeHtml = await homeResponse.Content.ReadAsStringAsync();
        Assert.Contains("Painel — André Gas e Água", homeHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsErrorAndDoesNotAuthenticate()
    {
        using var client = fixture.CreateClient();

        var (token, handler) = await GetAntiforgeryFieldsAsync(client, "/login");

        using var loginResponse = await PostLoginAsync(client, token, handler, AdminEmail, "SenhaErrada123");

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var html = await loginResponse.Content.ReadAsStringAsync();
        Assert.Contains("E-mail ou senha inv", html, StringComparison.Ordinal);

        // Still unauthenticated: hitting home should redirect to login again.
        using var homeResponse = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, homeResponse.StatusCode);
    }

    private static async Task<(string Token, string Handler)> GetAntiforgeryFieldsAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"").Groups[1].Value;
        var handler = Regex.Match(html, "name=\"_handler\" value=\"([^\"]*)\"").Groups[1].Value;

        Assert.False(string.IsNullOrEmpty(token));
        Assert.False(string.IsNullOrEmpty(handler));

        return (token, handler);
    }

    private static Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string token, string handler, string email, string password)
    {
        var form = new Dictionary<string, string>
        {
            ["_handler"] = handler,
            ["__RequestVerificationToken"] = token,
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["Input.RememberMe"] = "true",
        };

        return client.PostAsync("/login", new FormUrlEncodedContent(form));
    }
}