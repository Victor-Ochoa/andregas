using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Tenancy;
using AndreGas.IntegrationTests.TestHelpers;

using Aspire.Hosting;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AndreGas.IntegrationTests.Tests;

/// <summary>
/// Sobe apenas o Postgres real (via o mesmo AppHost usado em produção) para testar handlers que
/// usam <see cref="AppDbContext"/> diretamente, sem o overhead de subir o processo web inteiro.
/// </summary>
/// <remarks>
/// <para>
/// Multi-empresa: o contexto padrão desta fixture é <b>filtrado pela empresa padrão dos testes</b>
/// (<see cref="EmpresaIdPadrao"/>, o mesmo <c>EmpresaId</c> de
/// <see cref="TenantIntegracao.EmpresaId"/>). Assim os testes existentes exercitam o filtro global
/// sem precisar passar o tenant em cada <c>CreateDbContext()</c>.
/// </para>
/// <para>
/// Para ver todas as empresas (preparação/inspeção cross-tenant) use
/// <see cref="CreateDbContextCentral"/>; para agir como outra empresa, use
/// <see cref="CreateDbContext(Guid?)"/> com o id dela.
/// </para>
/// </remarks>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private const string IdentificadorEmpresaPadrao = "empresa-padrao-testes";

    private readonly Guid _empresaIdPadrao = TenantIntegracao.EmpresaId;

    private DistributedApplication _app = null!;
    private string _connectionString = null!;

    /// <summary>Connection string do Postgres real gerenciado pelo Aspire.</summary>
    public string ConnectionString => _connectionString;

    /// <summary>
    /// Empresa padrão dos testes de integração — a mesma referenciada por
    /// <see cref="TenantIntegracao.EmpresaId"/>. Garantida (linha em <c>Empresas</c>) por
    /// <see cref="ResetDatabaseAsync"/>.
    /// </summary>
    public Guid EmpresaIdPadrao => _empresaIdPadrao;

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

    /// <summary>
    /// Cria um <see cref="AppDbContext"/> filtrado pela <b>empresa padrão dos testes</b>
    /// (<see cref="EmpresaIdPadrao"/>), garantindo que as migrações estejam aplicadas. É o modo
    /// normal de teste: as linhas gravadas por este contexto são tagueadas com a empresa padrão
    /// pelo <see cref="SetEmpresaIdInterceptor"/> e as leituras só enxergam essa empresa.
    /// </summary>
    public AppDbContext CreateDbContext() => CreateDbContext(EmpresaIdPadrao);

    /// <summary>
    /// Cria um <see cref="AppDbContext"/> de uma empresa específica. <c>null</c> equivale ao modo
    /// central (vê todas as empresas) — prefira <see cref="CreateDbContextCentral"/> por clareza.
    /// </summary>
    public AppDbContext CreateDbContext(Guid? empresaId)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_connectionString);

        // O interceptor é o único lugar que preenche `EmpresaId` na escrita (as entidades têm
        // setter privado). Ele é instalado SEMPRE — inclusive no modo central (`empresaId` null):
        // nesse caso `EmpresaIdRequerida` lança em qualquer `Added` de entidade de domínio, o que é
        // a guarda desejada. Sem o interceptor, uma escrita central gravaria `EmpresaId = Guid.Empty`
        // (não há FK para `Empresas`) e a linha ficaria invisível para todos os tenants — fazendo
        // qualquer assert de ausência passar por vacuidade.
        var tenant = new FakeTenantContext(empresaId);
        builder.AddInterceptors(new SetEmpresaIdInterceptor(tenant));

        var context = new AppDbContext(builder.Options, tenant);
        context.Database.Migrate();
        return context;
    }

    /// <summary>
    /// Cria um <see cref="AppDbContext"/> em <b>modo central</b> (sem filtro global por empresa).
    /// Use só para preparação/inspeção cross-tenant — ex.: criar empresas reais, verificar dados de
    /// outra empresa ou limpar o banco inteiro.
    /// </summary>
    /// <remarks>
    /// <b>Somente leitura sobre dados de domínio.</b> O <see cref="SetEmpresaIdInterceptor"/> está
    /// instalado e, sem empresa selecionada, lança em qualquer <c>Added</c> de entidade de domínio —
    /// de propósito: uma linha gravada assim ficaria com <c>EmpresaId = Guid.Empty</c> e invisível
    /// para todos os tenants. Para criar dados de uma empresa, use
    /// <see cref="CreateDbContext(Guid?)"/> com o id dela (ou <c>EmpresaTestHelper</c>).
    /// </remarks>
    public AppDbContext CreateDbContextCentral() => CreateDbContext(null);

    /// <summary>
    /// Limpa todas as tabelas de domínio, na ordem correta para respeitar as foreign keys.
    /// O Postgres do AppHost usa um volume persistente (para sobreviver a restarts do
    /// `dotnet run` local), então diferentes classes de teste que usam esta fixture acabam
    /// compartilhando os mesmos dados entre execuções — cada teste deve chamar isto no seu
    /// <c>InitializeAsync</c> para garantir um estado limpo, independentemente do que outra
    /// classe de teste tenha deixado no banco.
    /// </summary>
    /// <remarks>
    /// Usa a visão <b>central</b> (precisa apagar dados de todas as empresas) e, ao final, garante
    /// a linha da empresa padrão dos testes — <c>AspNetUsers.EmpresaId</c> tem FK Restrict para
    /// <c>Empresas</c>, então sem ela qualquer teste que crie usuário com empresa falharia.
    /// </remarks>
    public async Task ResetDatabaseAsync()
    {
        using var db = CreateDbContextCentral();

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

        // Empresa padrão dos testes: precisa existir ANTES de qualquer teste criar usuário com
        // `EmpresaId` (FK Restrict de AspNetUsers). Inserida por SQL direto porque o Id é o GUID
        // fixo de TenantIntegracao (a entidade gera o Id no construtor).
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "Empresas" ("Id", "Identificador", "Nome", "Telefones", "Ativo", "CriadaEm")
            VALUES ({0}, {1}, {2}, '', true, {3})
            """,
            TenantIntegracao.EmpresaId,
            IdentificadorEmpresaPadrao,
            "Empresa Padrão dos Testes",
            DateTime.UtcNow);
    }
}