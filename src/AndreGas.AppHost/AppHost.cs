var builder = DistributedApplication.CreateBuilder(args);

// Senha fixa para o usuário postgres em desenvolvimento. O volume de dados do Postgres
// (WithDataVolume) persiste entre execuções, então uma senha gerada aleatoriamente a cada
// "dotnet run" pararia de bater com a senha já gravada no volume, causando falha de
// autenticação. Fixar a senha aqui garante que ela permaneça estável entre execuções.
var postgresPassword = builder.AddParameter("postgres-password", secret: true, value: "andregas-dev-only");

var postgres = builder.AddPostgres("postgres", password: postgresPassword)
    .WithDataVolume(isReadOnly: false)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgAdmin();

var andreGasDb = postgres.AddDatabase("andregas");

builder.AddProject<Projects.AndreGas_Web>("web")
    .WithReference(andreGasDb)
    .WaitFor(andreGasDb)
    .WithExternalHttpEndpoints();

builder.Build().Run();
