using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.UnitTests.TestHelpers;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.UnitTests.Tenancy;

/// <summary>
/// Testes do isolamento por empresa (multi-tenant) no <c>AppDbContext</c>: o filtro global garante
/// que um contexto com tenant só enxerga dados da própria empresa; sem tenant (central) enxerga
/// tudo; e operar domínio sem empresa selecionada lança via <c>EmpresaIdRequerida</c>.
/// </summary>
public class TenantIsolationTests
{
    private static readonly Guid EmpresaA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid EmpresaB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task Contexto_empresaA_nao_ve_dados_da_empresaB()
    {
        var banco = Guid.NewGuid().ToString();

        // Cada empresa grava um produto e um cliente no MESMO banco físico, cada um no seu tenant.
        await using (var contextoA = InMemoryDbContextFactory.Create(EmpresaA, banco))
        {
            contextoA.Produtos.Add(new Produto("Botijão da A", TipoProduto.Gas, 100m, 60m, 80m));
            contextoA.Clientes.Add(new Cliente("Cliente da A", "11911110000", "Rua A, 1"));
            await contextoA.SaveChangesAsync();
        }

        await using (var contextoB = InMemoryDbContextFactory.Create(EmpresaB, banco))
        {
            contextoB.Produtos.Add(new Produto("Botijão da B", TipoProduto.Gas, 200m, 120m, 160m));
            contextoB.Clientes.Add(new Cliente("Cliente da B", "11922220000", "Rua B, 2"));
            await contextoB.SaveChangesAsync();
        }

        // O contexto de A só enxerga o produto/cliente de A.
        await using (var contextoA = InMemoryDbContextFactory.Create(EmpresaA, banco))
        {
            var produtos = await contextoA.Produtos.ToListAsync();
            var clientes = await contextoA.Clientes.ToListAsync();

            Assert.Single(produtos);
            Assert.Equal("Botijão da A", produtos[0].Nome);
            Assert.Equal(EmpresaA, produtos[0].EmpresaId);

            Assert.Single(clientes);
            Assert.Equal("Cliente da A", clientes[0].Nome);
            Assert.Equal(EmpresaA, clientes[0].EmpresaId);
        }

        // E o contexto de B só enxerga o de B (não vaza nem na direção contrária).
        await using (var contextoB = InMemoryDbContextFactory.Create(EmpresaB, banco))
        {
            var produtos = await contextoB.Produtos.ToListAsync();
            var clientes = await contextoB.Clientes.ToListAsync();

            Assert.Single(produtos);
            Assert.Equal("Botijão da B", produtos[0].Nome);
            Assert.Equal(EmpresaB, produtos[0].EmpresaId);

            Assert.Single(clientes);
            Assert.Equal("Cliente da B", clientes[0].Nome);
            Assert.Equal(EmpresaB, clientes[0].EmpresaId);
        }
    }

    [Fact]
    public async Task Contexto_central_null_ve_tudo()
    {
        var banco = Guid.NewGuid().ToString();

        await using (var contextoA = InMemoryDbContextFactory.Create(EmpresaA, banco))
        {
            contextoA.Produtos.Add(new Produto("Botijão da A", TipoProduto.Gas, 100m, 60m, 80m));
            await contextoA.SaveChangesAsync();
        }

        await using (var contextoB = InMemoryDbContextFactory.Create(EmpresaB, banco))
        {
            contextoB.Produtos.Add(new Produto("Botijão da B", TipoProduto.Gas, 200m, 120m, 160m));
            await contextoB.SaveChangesAsync();
        }

        // Sem tenant (modo central): enxerga os produtos das duas empresas.
        await using var central = InMemoryDbContextFactory.Create(empresaId: null, banco);
        var produtos = await central.Produtos.OrderBy(p => p.Nome).ToListAsync();

        Assert.Equal(2, produtos.Count);
        Assert.Contains(produtos, p => p.EmpresaId == EmpresaA);
        Assert.Contains(produtos, p => p.EmpresaId == EmpresaB);
    }

    [Fact]
    public async Task Contexto_sem_tenant_adiciona_dado_de_dominio_lanca()
    {
        // Tenant presente porém sem empresa selecionada (central/super admin) — gravar dado de
        // domínio precisa falhar com a mensagem de EmpresaIdRequerida.
        var tenantSemEmpresa = new FakeTenantContext(empresaId: null, ehSuperAdmin: true);

        var options = new DbContextOptionsBuilder<AndreGas.Infrastructure.AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AndreGas.Infrastructure.Tenancy.SetEmpresaIdInterceptor(tenantSemEmpresa))
            .Options;

        await using var db = new AndreGas.Infrastructure.AppDbContext(options, tenantSemEmpresa);
        db.Produtos.Add(new Produto("Produto órfão", TipoProduto.Gas, 100m, 60m, 80m));

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());

        Assert.Contains("exige uma empresa", excecao.Message);
    }
}