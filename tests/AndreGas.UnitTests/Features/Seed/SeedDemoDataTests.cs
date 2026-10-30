using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Identity;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Seed;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AndreGas.UnitTests.Features.Seed;

/// <summary>
/// Testes do seed de demonstração (multi-empresa): a empresa demo, o admin dela e os dados de
/// domínio precisam nascer sob o tenant da empresa demo — nunca em modo central
/// (<c>EmpresaId = Guid.Empty</c>).
/// </summary>
public class SeedDemoDataTests
{
    // Data fixa para o teste: 15/09/2026 14:00 UTC.
    private static readonly DateTime FixedNow = new(2026, 9, 15, 14, 0, 0, DateTimeKind.Utc);
    private static readonly TimeProvider FixedTime = new FixedTimeProvider(FixedNow);

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    [Fact]
    public async Task SeedDemoData_cria_empresa_demo_e_admin_demo()
    {
        var banco = Guid.NewGuid().ToString();

        await ExecutarSeedAsync(banco);

        await using var db = InMemoryDbContextFactory.Create(empresaId: null, nomeBanco: banco);

        // Empresa demo criada com dados de cadastro plausíveis.
        var empresaDemo = await db.Set<Empresa>().AsNoTracking()
            .SingleAsync(e => e.Identificador == SeedDemoData.IdentificadorEmpresaDemo);
        Assert.Equal("Demo Ltda", empresaDemo.Nome);
        Assert.True(empresaDemo.Ativo);
        Assert.False(string.IsNullOrWhiteSpace(empresaDemo.Cnpj));
        Assert.NotEmpty(empresaDemo.Telefones);
        Assert.False(string.IsNullOrWhiteSpace(empresaDemo.Endereco));

        // Produtos, clientes e vendas pertencem à empresa demo (nunca a Guid.Empty / central).
        var produtos = await db.Produtos.AsNoTracking().ToListAsync();
        Assert.NotEmpty(produtos);
        Assert.All(produtos, p => Assert.Equal(empresaDemo.Id, p.EmpresaId));

        var clientes = await db.Clientes.AsNoTracking().ToListAsync();
        Assert.NotEmpty(clientes);
        Assert.All(clientes, c => Assert.Equal(empresaDemo.Id, c.EmpresaId));

        var vendas = await db.Vendas.AsNoTracking().ToListAsync();
        Assert.NotEmpty(vendas);
        Assert.All(vendas, v => Assert.Equal(empresaDemo.Id, v.EmpresaId));

        // Admin da empresa demo: existe, aponta para a empresa demo, ativo e na role Admin.
        var (userManager, _, provider) = CriarIdentityEmMemoria(banco);
        try
        {
            var adminDemo = await userManager.FindByEmailAsync(SeedDemoData.EmailAdminDemo);
            Assert.NotNull(adminDemo);
            Assert.Equal(empresaDemo.Id, adminDemo!.EmpresaId);
            Assert.True(adminDemo.Ativo);
            Assert.True(await userManager.IsInRoleAsync(adminDemo, ApplicationRole.Admin));
        }
        finally
        {
            provider.Dispose();
        }
    }

    [Fact]
    public async Task SeedAsync_DeveCriarProdutosClientesVendasEPagamentos()
    {
        var banco = Guid.NewGuid().ToString();

        await using var db = await ExecutarSeedAsync(banco);

        // Produtos com estoque cadastrado.
        var produtos = await db.Produtos.AsNoTracking().ToListAsync();
        Assert.NotEmpty(produtos);
        Assert.All(produtos, p => Assert.True(p.QuantidadeEstoque > 0, $"{p.Nome} sem estoque"));
        Assert.Contains(produtos, p => p.Tipo == TipoProduto.Gas);
        Assert.Contains(produtos, p => p.Tipo == TipoProduto.Agua);
        Assert.Contains(produtos, p => p.Tipo == TipoProduto.Outros);

        // Clientes variados.
        var clientes = await db.Clientes.AsNoTracking().ToListAsync();
        Assert.NotEmpty(clientes);
        Assert.All(clientes, c => Assert.False(string.IsNullOrWhiteSpace(c.Telefone)));

        // Vendas cobrindo TODAS as formas de pagamento.
        var vendas = await db.Vendas.AsNoTracking().Include(v => v.Itens).ToListAsync();
        Assert.NotEmpty(vendas);
        foreach (var forma in Enum.GetValues<FormaPagamento>())
            Assert.Contains(vendas, v => v.FormaPagamento == forma);

        // Ao menos uma venda fiado aberta e uma fiado quitada.
        Assert.Contains(vendas, v => v.Status == VendaStatus.FiadoAberto);
        Assert.Contains(vendas, v => v.Status == VendaStatus.FiadoQuitado);

        // Todas as vendas com data anterior a hoje.
        Assert.All(vendas, v => Assert.True(v.DataHora < FixedNow));

        // Vendas não-fiado geram Pagamento.
        var pagamentos = await db.Pagamentos.AsNoTracking().ToListAsync();
        Assert.NotEmpty(pagamentos);
        var vendasPagas = vendas.Where(v => v.FormaPagamento != FormaPagamento.Fiado).ToList();
        Assert.True(pagamentos.Count >= vendasPagas.Count);

        // O pagamento de uma venda paga no ato deve ter a MESMA data/hora da venda (não "agora"),
        // para que o pagamento "bata" com a venda nos dashboards por período.
        foreach (var venda in vendasPagas)
        {
            var pagamento = pagamentos.SingleOrDefault(p => p.ClienteId == venda.ClienteId
                && p.Valor == venda.ValorTotal
                && p.FormaPagamento == venda.FormaPagamento);
            Assert.NotNull(pagamento);
            Assert.Equal(venda.DataHora, pagamento!.Data);
        }

        // Movimentações de estoque (entrada + saída por venda).
        var movs = await db.MovimentacoesEstoque.AsNoTracking().ToListAsync();
        Assert.Contains(movs, m => m.Tipo == TipoMovimentacaoEstoque.Entrada);
        Assert.Contains(movs, m => m.Tipo == TipoMovimentacaoEstoque.Saida);

        // Saldo devedor: cliente com fiado aberto tem saldo > 0.
        Assert.Contains(clientes, c => c.SaldoDevedor > 0);
    }

    [Fact]
    public async Task SeedAsync_DeveSerIdempotente_NaoDuplicarDadosAoExecutarDuasVezes()
    {
        var banco = Guid.NewGuid().ToString();

        await ExecutarSeedAsync(banco);

        await using var db = InMemoryDbContextFactory.Create(empresaId: null, nomeBanco: banco);

        var qtdEmpresas = await db.Set<Empresa>().CountAsync();
        var qtdClientes = await db.Clientes.CountAsync();
        var qtdProdutos = await db.Produtos.CountAsync();
        var qtdVendas = await db.Vendas.CountAsync();

        await ExecutarSeedAsync(banco);

        Assert.Equal(qtdEmpresas, await db.Set<Empresa>().CountAsync());
        Assert.Equal(qtdClientes, await db.Clientes.CountAsync());
        Assert.Equal(qtdProdutos, await db.Produtos.CountAsync());
        Assert.Equal(qtdVendas, await db.Vendas.CountAsync());

        // O admin demo também não é duplicado (o e-mail é único no Identity).
        var (userManager, _, provider) = CriarIdentityEmMemoria(banco);
        try
        {
            var adminsDemo = await userManager.Users.Where(u => u.Email == SeedDemoData.EmailAdminDemo).ToListAsync();
            Assert.Single(adminsDemo);
        }
        finally
        {
            provider.Dispose();
        }
    }

    [Fact]
    public async Task SeedAsync_QuandoBancoJaTemProdutoLegadoSemEmpresa_DeveSemearNaEmpresaDemo()
    {
        // Linha legada (anterior à migração multi-empresa): EmpresaId = Guid.Empty, visível só no
        // modo central. Ela NÃO pode bloquear o seed — o guard de idempotência é por empresa.
        var banco = Guid.NewGuid().ToString();
        await using (var central = InMemoryDbContextFactory.Create(empresaId: null, nomeBanco: banco))
        {
            central.Produtos.Add(new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m));
            await central.SaveChangesAsync();
        }

        await using var db = await ExecutarSeedAsync(banco);

        var empresaDemo = await db.Set<Empresa>().AsNoTracking()
            .SingleAsync(e => e.Identificador == SeedDemoData.IdentificadorEmpresaDemo);

        var produtos = await db.Produtos.AsNoTracking().ToListAsync();

        // O produto legado continua lá (intocado, EmpresaId = Guid.Empty)...
        Assert.Contains(produtos, p => p.Nome == "Botijão 13kg" && p.EmpresaId == Guid.Empty);

        // ...e os produtos da demonstração foram criados sob a empresa demo.
        var produtosDemo = produtos.Where(p => p.EmpresaId == empresaDemo.Id).ToList();
        Assert.NotEmpty(produtosDemo);
        Assert.Contains(produtosDemo, p => p.Nome == "Botijão Gás 13kg");
    }

    [Fact]
    public async Task SeedAsync_DeveCriarVendasComValorDeEntrega()
    {
        var banco = Guid.NewGuid().ToString();

        await using var db = await ExecutarSeedAsync(banco);

        var vendasComEntrega = await db.Vendas.AsNoTracking()
            .Where(v => v.ValorEntrega > 0)
            .ToListAsync();

        // O seed aplica entrega em algumas vendas (demonstração do faturamento).
        Assert.NotEmpty(vendasComEntrega);
    }

    [Fact]
    public async Task SeedAsync_DeveCriarRecorrenciasDeRecompra_ParaProdutosComAvisarRecompra()
    {
        var banco = Guid.NewGuid().ToString();

        await using var db = await ExecutarSeedAsync(banco);

        // Produtos do seed marcados com AvisarRecompra devem ter recorrência para clientes que compraram.
        var produtosComAviso = await db.Produtos.AsNoTracking()
            .Where(p => p.AvisarRecompra)
            .ToListAsync();
        Assert.NotEmpty(produtosComAviso);

        foreach (var produto in produtosComAviso)
        {
            // Ao menos um cliente tem recorrência para este produto, com intervalo aprendido.
            var recorrencia = await db.RecorrenciasCompra.AsNoTracking()
                .FirstOrDefaultAsync(r => r.ProdutoId == produto.Id);
            Assert.NotNull(recorrencia);
            Assert.True(recorrencia!.IntervaloMedioDias > 0, $"{produto.Nome} sem intervalo de recompra.");
        }

        // Maria + Botijão 13kg: cliente com múltiplas vendas do produto → recorrência aprendida
        // (intervalo > 0 e mais de 1 compra, pois o seed espaça as vendas no tempo).
        var maria = await db.Clientes.AsNoTracking().SingleOrDefaultAsync(c => c.Nome == "Maria Souza");
        Assert.NotNull(maria);
        var botijao = await db.Produtos.AsNoTracking().SingleOrDefaultAsync(p => p.Nome == "Botijão Gás 13kg");
        Assert.NotNull(botijao);
        var recMaria = await db.RecorrenciasCompra.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ClienteId == maria!.Id && r.ProdutoId == botijao!.Id);
        Assert.NotNull(recMaria);
        Assert.True(recMaria!.IntervaloMedioDias > 0);
        Assert.True(recMaria.QuantidadeCompras >= 2);
    }

    /// <summary>
    /// Executa o seed sobre uma base InMemory compartilhada (como o startup faz com o Postgres) e
    /// devolve um contexto <b>central</b> (sem filtro de empresa) para inspecionar tudo o que foi
    /// gravado.
    /// </summary>
    private static async Task<AppDbContext> ExecutarSeedAsync(string banco)
    {
        var (userManager, roleManager, provider) = CriarIdentityEmMemoria(banco);
        try
        {
            await using var central = InMemoryDbContextFactory.Create(empresaId: null, nomeBanco: banco);

            await SeedDemoData.SeedAsync(
                central,
                empresaId => InMemoryDbContextFactory.Create(empresaId, banco),
                userManager,
                roleManager,
                FixedTime);
        }
        finally
        {
            provider.Dispose();
        }

        return InMemoryDbContextFactory.Create(empresaId: null, nomeBanco: banco);
    }

    /// <summary>
    /// Monta <c>UserManager</c>/<c>RoleManager</c> do Identity sobre a mesma base InMemory do seed,
    /// de forma fiel ao que o startup faz com o Postgres real.
    /// </summary>
    private static (UserManager<ApplicationUser> UserManager, RoleManager<ApplicationRole> RoleManager, ServiceProvider Provider)
        CriarIdentityEmMemoria(string banco)
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(banco));
        services
            .AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>();

        var provider = services.BuildServiceProvider();

        return (
            provider.GetRequiredService<UserManager<ApplicationUser>>(),
            provider.GetRequiredService<RoleManager<ApplicationRole>>(),
            provider);
    }
}