using Microsoft.EntityFrameworkCore;

using RevendaApp.Domain.Entities;
using RevendaApp.Domain.Enums;
using RevendaApp.Infrastructure;
using RevendaApp.IntegrationTests.TestHelpers;

namespace RevendaApp.IntegrationTests.Tests;

/// <summary>
/// Contrato do <see cref="DatabaseFixture.ResetDatabaseAsync"/>: depois do reset o banco fica
/// <b>vazio</b> (nenhuma linha de domínio, Identity ou auditoria sobrando) e existe exatamente a
/// empresa padrão dos testes — a linha exigida pela FK <c>AspNetUsers.EmpresaId</c>.
/// </summary>
/// <remarks>
/// <para>
/// Esta classe existe porque o reset era a origem provável de uma falha intermitente de FK
/// (<c>FK_RecorrenciasCompra_Produtos_ProdutoId</c>) observada quando o banco acumulava dados de
/// classes de teste anteriores. O teste popula <b>todas</b> as tabelas de uma vez — inclusive as
/// que referenciam outras por FK (recorrência → produto/cliente, empréstimo → venda/produto) — e
/// verifica que uma única chamada de reset limpa tudo.
/// </para>
/// <para>
/// Ele roda contra o Postgres real e não conhece a implementação: vale tanto para o reset por
/// <c>RemoveRange</c> quanto para o <c>TRUNCATE ... CASCADE</c> que o substituiu.
/// </para>
/// </remarks>
public class ResetDatabaseTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Reset_limpa_todas_as_tabelas_e_recria_a_empresa_padrao()
    {
        // Um reset antes de popular garante um cenário conhecido, mesmo com lixo de outra classe.
        await fixture.ResetDatabaseAsync();
        var emailDoUsuarioDeTeste = await PopularTodasAsTabelasAsync();

        // Sanidade: o cenário foi montado (sem isto o assert de "vazio" passaria por vacuidade).
        using (var db = fixture.CreateDbContextCentral())
        {
            Assert.NotEmpty(await db.Clientes.ToListAsync());
            Assert.NotEmpty(await db.Produtos.ToListAsync());
            Assert.NotEmpty(await db.Vendas.ToListAsync());
            Assert.NotEmpty(await db.ItensVenda.ToListAsync());
            Assert.NotEmpty(await db.Pagamentos.ToListAsync());
            Assert.NotEmpty(await db.MovimentacoesEstoque.ToListAsync());
            Assert.NotEmpty(await db.HistoricosEstoque.ToListAsync());
            Assert.NotEmpty(await db.PrecosPersonalizadosClientes.ToListAsync());
            Assert.NotEmpty(await db.EmprestimosVasilhames.ToListAsync());
            Assert.NotEmpty(await db.RecorrenciasCompra.ToListAsync());
            Assert.NotEmpty(await db.RegistrosAuditoria.ToListAsync());
            Assert.Contains(db.Users, u => u.Email == emailDoUsuarioDeTeste);
        }

        await fixture.ResetDatabaseAsync();

        using (var db = fixture.CreateDbContextCentral())
        {
            Assert.Empty(await db.Clientes.ToListAsync());
            Assert.Empty(await db.Produtos.ToListAsync());
            Assert.Empty(await db.Vendas.ToListAsync());
            Assert.Empty(await db.ItensVenda.ToListAsync());
            Assert.Empty(await db.Pagamentos.ToListAsync());
            Assert.Empty(await db.MovimentacoesEstoque.ToListAsync());
            Assert.Empty(await db.HistoricosEstoque.ToListAsync());
            Assert.Empty(await db.PrecosPersonalizadosClientes.ToListAsync());
            Assert.Empty(await db.EmprestimosVasilhames.ToListAsync());
            Assert.Empty(await db.RecorrenciasCompra.ToListAsync());
            Assert.Empty(await db.RegistrosAuditoria.ToListAsync());

            // O usuário criado pelo teste saiu. (Não se asserta "AspNetUsers vazio": o
            // DatabaseFixture sobe o AppHost inteiro, então o processo `web` roda o seed do super
            // admin em paralelo e pode recriá-lo — asserção de vazio seria uma corrida.)
            Assert.DoesNotContain(db.Users, u => u.Email == emailDoUsuarioDeTeste);

            // Só a empresa padrão permanece (a FK de AspNetUsers exige que ela exista).
            var empresa = Assert.Single(await db.Set<Empresa>().ToListAsync());
            Assert.Equal(TenantIntegracao.EmpresaId, empresa.Id);
        }

        // O TRUNCATE não pode encostar no histórico de migrations: sem ele o próximo Migrate()
        // tentaria reaplicar o schema inteiro.
        using var dbHistorico = fixture.CreateDbContextCentral();
        var migrations = await dbHistorico.Database
            .SqlQueryRaw<string>("SELECT \"MigrationId\" AS \"Value\" FROM \"__EFMigrationsHistory\"")
            .ToListAsync();
        Assert.NotEmpty(migrations);
    }

    /// <summary>
    /// Prova que o conjunto do <c>TRUNCATE ... CASCADE</c> é <b>fechado</b>: nenhuma tabela fora
    /// dele tem FK apontando para uma tabela de dentro. Sem isso o <c>CASCADE</c> poderia esvaziar
    /// uma tabela que o reset não pretende limpar (e o teste acima não perceberia, porque ele só
    /// olha as tabelas conhecidas).
    /// </summary>
    [Fact]
    public async Task Nenhuma_tabela_fora_do_reset_referencia_uma_tabela_do_reset()
    {
        // Lê a lista REAL do reset (fonte única na fixture). Se uma tabela sair do reset ou uma
        // migração futura adicionar tabela com FK para alguma destas, este teste fica vermelho e
        // avisa que o reset precisa ser atualizado.
        var tabelasDoReset = DatabaseFixture.TabelasDoReset;

        await fixture.ResetDatabaseAsync();

        using var db = fixture.CreateDbContextCentral();

        // Lê todos os pares (filha → pai) e filtra em C#: evita depender de binding de array no
        // provider e deixa o assert com a mensagem completa quando falhar.
        var relacoes = await db.Database
            .SqlQueryRaw<string>(
                """
                SELECT c.conrelid::regclass::text || ' -> ' || c.confrelid::regclass::text AS "Value"
                FROM pg_constraint c
                WHERE c.contype = 'f'
                """)
            .ToListAsync();

        // `regclass::text` devolve os nomes entre aspas quando o identificador é case-sensitive
        // (ex.: `"RecorrenciasCompra"`); a lista do reset usa o nome puro. Normaliza os dois lados.
        var doReset = tabelasDoReset
            .Select(t => t.Trim('"'))
            .ToHashSet(StringComparer.Ordinal);

        // Sanidade do próprio teste: a consulta precisa enxergar as FKs (senão o assert de vazio
        // abaixo passaria por vacuidade, como aconteceu quando o formato dos nomes divergiu).
        Assert.Contains(relacoes, r => r.Contains("ItensVenda", StringComparison.Ordinal));

        var referenciadoras = relacoes
            .Select(r => r.Split(" -> ", StringSplitOptions.TrimEntries))
            .Select(par => (Filha: par[0].Trim('"'), Pai: par[1].Trim('"')))
            .Where(par => doReset.Contains(par.Pai) && !doReset.Contains(par.Filha))
            .Select(par => par.Filha)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(referenciadoras);
    }

    /// <summary>
    /// Repete o ciclo popular → reset várias vezes seguidas. A falha intermitente de FK
    /// (<c>FK_RecorrenciasCompra_Produtos_ProdutoId</c>) só aparecia com o banco acumulando dados
    /// de classes anteriores; um único ciclo não reproduz essa condição. Este teste força a
    /// condição — estado sujo repetidamente — e falha se qualquer ciclo deixar resíduo.
    /// </summary>
    /// <remarks>
    /// Custo declarado: cada ciclo abre conexão e faz TRUNCATE, então são ~10x o teste simples.
    /// Vale porque a alternativa era registrar "mitigado, não provado" e descobrir a regressão
    /// numa execução futura sem contexto.
    /// </remarks>
    [Fact]
    public async Task Reset_e_repetivel_em_ciclos_consecutivos_sem_residuo()
    {
        const int ciclos = 10;

        for (var ciclo = 1; ciclo <= ciclos; ciclo++)
        {
            await fixture.ResetDatabaseAsync();
            await PopularTodasAsTabelasAsync();

            await fixture.ResetDatabaseAsync();

            using var db = fixture.CreateDbContextCentral();
            Assert.True(
                !await db.RecorrenciasCompra.AnyAsync()
                && !await db.Produtos.AnyAsync()
                && !await db.Clientes.AnyAsync()
                && !await db.Vendas.AnyAsync()
                && !await db.EmprestimosVasilhames.AnyAsync(),
                $"O ciclo {ciclo} de {ciclos} deixou resíduo no banco.");
        }
    }

    /// <summary>
    /// Popula todas as tabelas de domínio com um grafo completo, exercitando as FKs que o reset
    /// precisa respeitar (recorrência → cliente/produto; empréstimo → venda/produto/cliente).
    /// </summary>
    private async Task<string> PopularTodasAsTabelasAsync()
    {
        Guid clienteId;
        Guid produtoId;
        Guid vendaId;

        using (var db = fixture.CreateDbContext())
        {
            var tenant = TenantIntegracao.Contexto();

            clienteId = await new RevendaApp.Web.Features.Clientes.Cadastrar.CadastrarClienteCommandHandler(db, tenant)
                .Handle(new RevendaApp.Web.Features.Clientes.Cadastrar.CadastrarClienteCommand(
                    "Cliente Reset", "11977776666", "Rua Reset, 1"), CancellationToken.None);

            produtoId = await new RevendaApp.Web.Features.Estoque.Cadastrar.CadastrarProdutoCommandHandler(db, tenant)
                .Handle(new RevendaApp.Web.Features.Estoque.Cadastrar.CadastrarProdutoCommand(
                    "Botijão Reset", TipoProduto.Gas, 100m, 60m, 80m, 5,
                    ControlaVasilhame: true, QuantidadeVasilhamesInicial: 10), CancellationToken.None);

            await new RevendaApp.Web.Features.Estoque.Movimentar.RegistrarMovimentacaoEstoqueCommandHandler(db, tenant)
                .Handle(new RevendaApp.Web.Features.Estoque.Movimentar.RegistrarMovimentacaoEstoqueCommand(
                    produtoId, TipoMovimentacaoEstoque.Entrada, 10, "Estoque inicial"), CancellationToken.None);

            // Venda com item + pagamento + histórico do produto (os handlers criam o grafo).
            await new RevendaApp.Web.Features.Vendas.NovaVenda.RegistrarVendaCommandHandler(db, tenant)
                .Handle(new RevendaApp.Web.Features.Vendas.NovaVenda.RegistrarVendaCommand(
                    clienteId, "Cliente Reset", "11977776666", "Rua Reset, 1",
                    FormaPagamento.Dinheiro, 0m,
                    [new RevendaApp.Web.Features.Vendas.NovaVenda.ItemVendaInput(produtoId, 2)]),
                    CancellationToken.None);

            vendaId = (await db.Vendas.Select(v => v.Id).SingleAsync());

            // Empréstimo ligado à venda (FK VendaId) e recorrência ligada a cliente+produto.
            db.EmprestimosVasilhames.Add(new EmprestimoVasilhame(clienteId, produtoId, 1, vendaId: vendaId));
            db.PrecosPersonalizadosClientes.Add(new PrecoPersonalizadoCliente(clienteId, produtoId, 90m));
            db.RecorrenciasCompra.Add(new RecorrenciaCompra(clienteId, produtoId, DateTime.UtcNow, 7m, 1));
            db.RegistrosAuditoria.Add(new RegistroAuditoria(
                null, null, "sistema", RegistroAuditoria.Acoes.EmpresaEditada,
                "Registro de teste do reset", RegistroAuditoria.Origens.Central));

            await db.SaveChangesAsync();
        }

        // Identity (AspNetUsers/AspNetRoles/AspNetUserRoles): o reset precisa limpar também as
        // tabelas do Identity, que não são entidades de domínio.
        var email = $"reset-{Guid.NewGuid():N}@revenda.app.com.br";
        await EmpresaTestHelper.CriarEmpresaEAdminAsync(
            fixture.ConnectionString,
            "Empresa Reset Usuários",
            $"empresa-reset-{Guid.NewGuid():N}",
            email,
            "Senha@123");

        return email;
    }
}