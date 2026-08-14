var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();

var andreGasDb = postgres.AddDatabase("andregas");

builder.AddProject<Projects.AndreGas_Web>("web")
    .WithReference(andreGasDb)
    .WaitFor(andreGasDb)
    .WithExternalHttpEndpoints();

builder.Build().Run();
