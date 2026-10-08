var builder = DistributedApplication.CreateBuilder(args);

// Deploy remoto via SSH (aspire-ssh-deploy / docker compose): o comando `aspire deploy`
// gera docker-compose.yaml e faz push das imagens + compose up na VM (padrão CapZeroApp).
builder.AddDockerComposeEnvironment("env").WithSshDeploySupport();

// Segredos do admin usados no deploy. Em dev continuam os defaults do SeedData.
// O valor vem do appsettings.Production.json do AppHost (Parameters) ou env Parameters__*;
// NÃO fixamos default no código para o valor chegar ao compose no publish mode.
var seedAdminEmail = builder.AddParameter("seed-admin-email");
var seedAdminPassword = builder.AddParameter("seed-admin-password");

// Senha fixa para o usuário postgres em desenvolvimento. O volume de dados do Postgres
// (WithDataVolume) persiste entre execuções, então uma senha gerada aleatoriamente a cada
// "dotnet run" pararia de bater com a senha já gravada no volume, causando falha de
// autenticação. Fixar a senha aqui garante que ela permaneça estável entre execuções.
var postgresPassword = builder.AddParameter("postgres-password", secret: true, value: "andregas-dev-only");

var postgres = builder.AddPostgres("postgres", password: postgresPassword)
    .WithDataVolume(isReadOnly: false)
    .WithLifetime(ContainerLifetime.Persistent);

// Em dev, expõe o pgAdmin; em publish (deploy), não sobe e o Postgres expõe a porta TCP p/ o host.
if (builder.ExecutionContext.IsPublishMode)
{
    postgres.WithEndpoint("tcp", endpoint =>
    {
        endpoint.Port = 5432;
        endpoint.IsExternal = true;
    });
}
else
{
    var pgAdmin = postgres.WithPgAdmin(pgAdminBuilder =>
    {
        // A imagem pgAdmin4 (dpage/pgadmin4) escuta por padrão em IPv6 (lista "[::]"),
        // o que não responde na porta IPv4 que o Aspire publica no host — resultando em
        // "Running (Unhealthy)" e timeout no browser. Forçamos o bind para IPv4.
        pgAdminBuilder.WithEnvironment("PGADMIN_LISTEN_ADDRESS", "0.0.0.0");
    }, containerName: "pgadmin");
    // Fixa a porta do host (5050) para acesso estável ao pgAdmin em http://localhost:5050,
    // em vez da porta aleatória que o Aspire escolheria a cada "dotnet run".
    pgAdmin.WithHostPort(5050);
}

var andreGasDb = postgres.AddDatabase("andregas");

// Em publish, o Aspire define ASPNETCORE_URLS a partir dos endpoints Http; definimos também
// a env para garantir ambiente de produção (desliga o seed de demo e exige credenciais admin).
// No dev local (aspire run) NÃO forçamos Production: em Development o MapStaticAssets serve os
// assets de content roots corretamente; rodar o app não-publicado em Production faz o
// StaticAssetsInvoker procurar os arquivos no wwwroot físico (que não os contém) e quebrar o visual.
var web = builder.AddProject<Projects.AndreGas_Web>("web")
    .WithReference(andreGasDb)
    .WaitFor(andreGasDb)
    // Alinha o relógio do container com o horário de Brasília (app de balcão brasileiro) —
    // logs, DateTime.Now e TimeProvider.System do container passam a ver o TZ correto.
    .WithEnvironment("TZ", "America/Sao_Paulo")
    .WithEnvironment("SeedAdmin__Email", seedAdminEmail)
    .WithEnvironment("SeedAdmin__Password", seedAdminPassword);

if (builder.ExecutionContext.IsPublishMode)
    web.WithEnvironment("ASPNETCORE_ENVIRONMENT", "Production");

// Em publish mode o Caddy (host) faz a terminação TLS; o Kestrel não precisa expor HTTPS.
// A API do Aspire para desligar o cert é "experimental" — suprimimos o diagnóstico localmente.
#pragma warning disable ASPIRECERTIFICATES001
if (builder.ExecutionContext.IsPublishMode)
    web.WithoutHttpsCertificate();
#pragma warning restore ASPIRECERTIFICATES001

if (builder.ExecutionContext.IsPublishMode)
{
    web
        .WithEndpoint("http", endpoint =>
        {
            endpoint.Port = 8080;
            endpoint.IsExternal = true;
            endpoint.UriScheme = "http";
        })
        .WithEndpoint("https", endpoint =>
        {
            endpoint.Port = 8443;
            endpoint.IsExternal = true;
            endpoint.UriScheme = "https";
        })
        .WithExternalHttpEndpoints();
}
else
{
    web
        .WithEndpoint("http", endpoint =>
        {
            endpoint.Port = 5220;
            endpoint.IsExternal = true;
            endpoint.UriScheme = "http";
        })
        .WithExternalHttpEndpoints();
}

builder.Build().Run();