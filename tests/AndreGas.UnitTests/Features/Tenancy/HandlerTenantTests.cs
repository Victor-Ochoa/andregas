using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure.Identity;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Clientes.Cadastrar;
using AndreGas.Web.Features.Clientes.Listar;
using AndreGas.Web.Features.Estoque.Cadastrar;
using AndreGas.Web.Features.Estoque.Listar;
using AndreGas.Web.Features.Home;
using AndreGas.Web.Features.Usuarios.Listar;
using AndreGas.Web.Features.Vendas.NovaVenda;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.UnitTests.Features.Tenancy;

/// <summary>
/// Handlers de domínio exigem uma empresa (tenant) selecionada: em modo central (super admin sem
/// empresa, ou configuração inválida) a operação falha com <c>EmpresaIdRequerida</c> em vez de
/// rodar sobre os dados de todas as empresas. Também pina o isolamento funcional: cada handler só
/// enxerga/grava os dados da empresa do tenant corrente (via filtro global + interceptor).
/// </summary>
public class HandlerTenantTests
{
    private static readonly Guid EmpresaA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid EmpresaB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    /// <summary>Tenant existente porém sem empresa selecionada (super admin da central).</summary>
    private static FakeTenantContext TenantCentral() => new(empresaId: null, ehSuperAdmin: true);

    [Fact]
    public async Task CadastrarProduto_sem_empresa_selecionada_lanca()
    {
        var tenant = TenantCentral();
        await using var db = InMemoryDbContextFactory.Create(tenant: tenant);
        var handler = new CadastrarProdutoCommandHandler(db, tenant: tenant);

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(
                new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5),
                CancellationToken.None).AsTask());

        Assert.Contains("exige uma empresa", excecao.Message);
        Assert.Empty(db.Produtos);
    }

    [Fact]
    public async Task ListarProdutos_sem_empresa_selecionada_lanca()
    {
        var tenant = TenantCentral();
        await using var db = InMemoryDbContextFactory.Create(tenant: tenant);
        var handler = new ListarProdutosQueryHandler(db, tenant);

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ListarProdutosQuery(), CancellationToken.None).AsTask());

        Assert.Contains("exige uma empresa", excecao.Message);
    }

    [Fact]
    public async Task RegistrarVenda_sem_empresa_selecionada_lanca()
    {
        var tenant = TenantCentral();
        await using var db = InMemoryDbContextFactory.Create(tenant: tenant);
        var handler = new RegistrarVendaCommandHandler(db, tenant: tenant);

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(
                new RegistrarVendaCommand(
                    ClienteId: null,
                    NomeClienteNovo: "Maria",
                    TelefoneClienteNovo: "11999990000",
                    EnderecoClienteNovo: "Rua A, 1",
                    FormaPagamento: FormaPagamento.Dinheiro,
                    Desconto: 0m,
                    Itens: [new ItemVendaInput(Guid.NewGuid(), 1)]),
                CancellationToken.None).AsTask());

        Assert.Contains("exige uma empresa", excecao.Message);
        // A guarda dispara antes de qualquer escrita: nenhuma venda, item ou movimentação.
        Assert.Empty(db.Vendas);
        Assert.Empty(db.ItensVenda);
        Assert.Empty(db.MovimentacoesEstoque);
    }

    [Fact]
    public async Task CadastrarCliente_sem_empresa_selecionada_lanca()
    {
        var tenant = TenantCentral();
        await using var db = InMemoryDbContextFactory.Create(tenant: tenant);
        var handler = new CadastrarClienteCommandHandler(db, tenant);

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(
                new CadastrarClienteCommand("Maria", "11999990000", "Rua A, 1"),
                CancellationToken.None).AsTask());

        Assert.Contains("exige uma empresa", excecao.Message);
        Assert.Empty(db.Clientes);
    }

    [Fact]
    public async Task ObterDashboard_sem_empresa_selecionada_lanca()
    {
        var tenant = TenantCentral();
        await using var db = InMemoryDbContextFactory.Create(tenant: tenant);
        var handler = new ObterDashboardQueryHandler(db, tenant, TimeProvider.System);

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ObterDashboardQuery(), CancellationToken.None).AsTask());

        Assert.Contains("exige uma empresa", excecao.Message);
    }

    [Fact]
    public async Task ListarClientes_retorna_apenas_clientes_da_empresa_do_tenant()
    {
        var banco = Guid.NewGuid().ToString();

        var tenantA = new FakeTenantContext(EmpresaA);
        var tenantB = new FakeTenantContext(EmpresaB);

        await using (var dbA = InMemoryDbContextFactory.Create(tenant: tenantA, nomeBanco: banco))
        {
            dbA.Clientes.Add(new Cliente("Cliente da A", "11911110000", "Rua A, 1"));
            await dbA.SaveChangesAsync();
        }

        await using (var dbB = InMemoryDbContextFactory.Create(tenant: tenantB, nomeBanco: banco))
        {
            dbB.Clientes.Add(new Cliente("Cliente da B", "11922220000", "Rua B, 2"));
            await dbB.SaveChangesAsync();
        }

        await using var dbConsulta = InMemoryDbContextFactory.Create(tenant: tenantA, nomeBanco: banco);
        var handler = new ListarClientesQueryHandler(dbConsulta, tenantA);

        var clientes = await handler.Handle(new ListarClientesQuery(), CancellationToken.None);

        var cliente = Assert.Single(clientes);
        Assert.Equal("Cliente da A", cliente.Nome);
    }

    [Fact]
    public async Task CadastrarCliente_grava_EmpresaId_do_tenant_corrente()
    {
        var tenant = new FakeTenantContext(EmpresaA);
        await using var db = InMemoryDbContextFactory.Create(tenant: tenant);
        var handler = new CadastrarClienteCommandHandler(db, tenant);

        var id = await handler.Handle(
            new CadastrarClienteCommand("Maria", "11999990000", "Rua A, 1"),
            CancellationToken.None);

        // O EmpresaId vem do interceptor (o handler não seta na mão).
        var cliente = await db.Clientes.SingleAsync(c => c.Id == id);
        Assert.Equal(EmpresaA, cliente.EmpresaId);
    }

    [Fact]
    public async Task ListarProdutos_nao_ve_produtos_de_outra_empresa()
    {
        var banco = Guid.NewGuid().ToString();

        var tenantA = new FakeTenantContext(EmpresaA);
        var tenantB = new FakeTenantContext(EmpresaB);

        await using (var dbA = InMemoryDbContextFactory.Create(tenant: tenantA, nomeBanco: banco))
        {
            dbA.Produtos.Add(new Produto("Botijão da A", TipoProduto.Gas, 100m, 60m, 80m));
            await dbA.SaveChangesAsync();
        }

        await using (var dbB = InMemoryDbContextFactory.Create(tenant: tenantB, nomeBanco: banco))
        {
            dbB.Produtos.Add(new Produto("Botijão da B", TipoProduto.Gas, 200m, 120m, 160m));
            await dbB.SaveChangesAsync();
        }

        await using var dbConsulta = InMemoryDbContextFactory.Create(tenant: tenantB, nomeBanco: banco);
        var handler = new ListarProdutosQueryHandler(dbConsulta, tenantB);

        var produtos = await handler.Handle(new ListarProdutosQuery(), CancellationToken.None);

        var produto = Assert.Single(produtos);
        Assert.Equal("Botijão da B", produto.Nome);
    }

    [Fact]
    public async Task ListarUsuarios_lista_apenas_usuarios_da_empresa_do_tenant()
    {
        var banco = Guid.NewGuid().ToString();

        var tenantA = new FakeTenantContext(EmpresaA);

        await using (var central = InMemoryDbContextFactory.Create(empresaId: null, banco))
        {
            central.Users.Add(new ApplicationUser
            {
                UserName = "admin@a.com",
                Email = "admin@a.com",
                Nome = "Admin A",
                EmpresaId = EmpresaA,
            });
            central.Users.Add(new ApplicationUser
            {
                UserName = "admin@b.com",
                Email = "admin@b.com",
                Nome = "Admin B",
                EmpresaId = EmpresaB,
            });
            await central.SaveChangesAsync();
        }

        await using var db = InMemoryDbContextFactory.Create(tenant: tenantA, nomeBanco: banco);
        var handler = new ListarUsuariosQueryHandler(db, tenantA);

        var usuarios = await handler.Handle(new ListarUsuariosQuery(), CancellationToken.None);

        var usuario = Assert.Single(usuarios);
        Assert.Equal("admin@a.com", usuario.Email);
    }

    [Fact]
    public async Task ListarUsuarios_sem_empresa_selecionada_lanca()
    {
        var tenant = TenantCentral();
        await using var db = InMemoryDbContextFactory.Create(tenant: tenant);
        var handler = new ListarUsuariosQueryHandler(db, tenant);

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ListarUsuariosQuery(), CancellationToken.None).AsTask());

        Assert.Contains("exige uma empresa", excecao.Message);
    }
}