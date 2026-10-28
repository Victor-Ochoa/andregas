using AndreGas.Infrastructure;

using Aspire.Hosting;

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

    /// <summary>Connection string do Postgres real gerenciado pelo Aspire.</summary>
    public string ConnectionString => _connectionString;

    public async Task InitializeAsync()
    {
        var cancellationToken = CancellationToken.None;

        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AndreGas_AppHost>(cancellationToken);

        // O AppHost registra seed-admin-email/seed-admin-password como Parameters sem default
        // (valor vem de config em produção). Sob o Aspire.Hosting.Testing, prover valores evita
        // MissingParameterValueException no build/start do AppHost.
        appHost.Configuration["Parameters:seed-admin-email"] = "admin@andregas.com.br";
        appHost.Configuration["Parameters:seed-admin-password"] = "AndreGas@123";

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

    /// <summary>
    /// Limpa todas as tabelas de domínio, na ordem correta para respeitar as foreign keys.
    /// O Postgres do AppHost usa um volume persistente (para sobreviver a restarts do
    /// `dotnet run` local), então diferentes classes de teste que usam esta fixture acabam
    /// compartilhando os mesmos dados entre execuções — cada teste deve chamar isto no seu
    /// <c>InitializeAsync</c> para garantir um estado limpo, independentemente do que outra
    /// classe de teste tenha deixado no banco.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        using var db = CreateDbContext();

        // Ordem: primeiro as tabelas que referenciam outras (filhas), depois as referenciadas.
        // ItensVenda/Pagamentos → Vendas/Produtos; EmprestimosVasilhames/Precos/Recorrencias →
        // Clientes+Produtos; por fim Produtos e Clientes (não são referenciados por ninguém).
        db.ItensVenda.RemoveRange(db.ItensVenda);
        db.Pagamentos.RemoveRange(db.Pagamentos);
        db.Vendas.RemoveRange(db.Vendas);
        db.EmprestimosVasilhames.RemoveRange(db.EmprestimosVasilhames);
        db.MovimentacoesEstoque.RemoveRange(db.MovimentacoesEstoque);
        db.HistoricosEstoque.RemoveRange(db.HistoricosEstoque);
        db.PrecosPersonalizadosClientes.RemoveRange(db.PrecosPersonalizadosClientes);
        db.RecorrenciasCompra.RemoveRange(db.RecorrenciasCompra);
        db.Produtos.RemoveRange(db.Produtos);
        db.Clientes.RemoveRange(db.Clientes);

        await db.SaveChangesAsync();

        // Identity: as tabelas de junção/claims referenciam AspNetUsers e AspNetRoles;
        // AspNetUsers.EmpresaId referencia Empresas (FK Restrict), então os usuários saem antes
        // das empresas. Feito via SQL direto porque o Identity não expõe DbSets aqui e porque
        // `Empresas` não é entidade de domínio filtrada.
        await db.Database.ExecuteSqlRawAsync(
            """
            DELETE FROM "AspNetUserRoles";
            DELETE FROM "AspNetUserClaims";
            DELETE FROM "AspNetUserLogins";
            DELETE FROM "AspNetUserTokens";
            DELETE FROM "AspNetRoleClaims";
            DELETE FROM "AspNetUsers";
            DELETE FROM "AspNetRoles";
            DELETE FROM "Empresas";
            """);
    }
}