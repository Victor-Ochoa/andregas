using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.IntegrationTests.TestHelpers;
using AndreGas.Web.Features.Clientes.Cadastrar;
using AndreGas.Web.Features.Clientes.Emprestimos;
using AndreGas.Web.Features.Clientes.Listar;
using AndreGas.Web.Features.Estoque.Cadastrar;
using AndreGas.Web.Features.Estoque.Listar;
using AndreGas.Web.Features.Estoque.Movimentar;
using AndreGas.Web.Features.Usuarios.Listar;
using AndreGas.Web.Features.Vendas.NovaVenda;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.IntegrationTests.Tests;

/// <summary>
/// Prova o <b>isolamento por empresa</b> (multi-tenant) contra o Postgres real, com duas empresas
/// de verdade e contextos filtrados pelo tenant de cada uma.
/// </summary>
/// <remarks>
/// <para>
/// Diferente dos demais testes de feature — que rodam na empresa padrão
/// (<see cref="TenantIntegracao.EmpresaId"/>) e por isso não conseguem distinguir "filtro
/// funcionando" de "só existe uma empresa" — aqui existem dados nas duas empresas. Cada teste tem
/// <b>controle positivo</b>: o dado aparece na própria empresa; o assert de ausência na outra
/// empresa só vale junto com ele (sem isso o teste seria vacuoso, passando mesmo com o filtro
/// quebrado).
/// </para>
/// <para>
/// <b>Exceção:</b> <c>Admin_de_uma_empresa_nao_aparece_na_listagem_de_usuarios_de_outra</c> não
/// exercita o filtro global — o Identity (<c>AspNetUsers</c>) não é filtrado por empresa. Ele
/// valida o filtro <b>explícito</b> do <c>ListarUsuariosQueryHandler</c>, que é a única barreira
/// ali.
/// </para>
/// <para>
/// As empresas são criadas via <c>CreateDbContextCentral()</c> (a visão central é necessária para
/// criar tenants) e os dados de domínio sempre por um contexto <b>com o tenant</b> da empresa
/// dona — é o <c>SetEmpresaIdInterceptor</c> que tagueia as linhas.
/// </para>
/// </remarks>
public class TenancyIsolationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private Guid _empresaA;
    private Guid _empresaB;

    public async Task InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();

        await using var db = fixture.CreateDbContextCentral();
        var empresaA = await EmpresaTestHelper.CriarEmpresaAsync(db, "Empresa A Isolamento");
        var empresaB = await EmpresaTestHelper.CriarEmpresaAsync(db, "Empresa B Isolamento");

        _empresaA = empresaA.Id;
        _empresaB = empresaB.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Clientes_com_mesmo_telefone_em_empresas_diferentes_nao_colidem()
    {
        // O telefone é único por empresa (índice composto EmpresaId+Telefone): o mesmo número pode
        // existir nas duas empresas, e cada listagem só enxerga o cliente da sua empresa.
        const string telefone = "11955554444";

        using (var dbA = fixture.CreateDbContext(_empresaA))
        {
            await new CadastrarClienteCommandHandler(dbA, new FakeTenantContext(_empresaA))
                .Handle(new CadastrarClienteCommand("Cliente da A", telefone, "Rua A, 1"), CancellationToken.None);
        }

        using (var dbB = fixture.CreateDbContext(_empresaB))
        {
            await new CadastrarClienteCommandHandler(dbB, new FakeTenantContext(_empresaB))
                .Handle(new CadastrarClienteCommand("Cliente da B", telefone, "Rua B, 2"), CancellationToken.None);
        }

        // Controle positivo + isolamento: cada empresa enxerga exatamente o seu cliente.
        using (var dbA = fixture.CreateDbContext(_empresaA))
        {
            var clientesA = await new ListarClientesQueryHandler(dbA, new FakeTenantContext(_empresaA))
                .Handle(new ListarClientesQuery(), CancellationToken.None);

            var cliente = Assert.Single(clientesA);
            Assert.Equal("Cliente da A", cliente.Nome);
            Assert.Equal(telefone, cliente.Telefone);
        }

        using (var dbB = fixture.CreateDbContext(_empresaB))
        {
            var clientesB = await new ListarClientesQueryHandler(dbB, new FakeTenantContext(_empresaB))
                .Handle(new ListarClientesQuery(), CancellationToken.None);

            var cliente = Assert.Single(clientesB);
            Assert.Equal("Cliente da B", cliente.Nome);
            Assert.Equal(telefone, cliente.Telefone);
        }

        // Sanidade do cenário: as duas linhas existem de fato (visão central).
        using var dbCentral = fixture.CreateDbContextCentral();
        Assert.Equal(2, await dbCentral.Clientes.CountAsync(c => c.Telefone == telefone));
    }

    [Fact]
    public async Task Produto_de_A_nao_aparece_na_listagem_de_B()
    {
        Guid produtoAId;
        using (var dbA = fixture.CreateDbContext(_empresaA))
        {
            produtoAId = await new CadastrarProdutoCommandHandler(dbA, new FakeTenantContext(_empresaA))
                .Handle(new CadastrarProdutoCommand("Botijão da A", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);
        }

        // Controle positivo: a empresa dona vê o produto.
        using (var dbA = fixture.CreateDbContext(_empresaA))
        {
            var produtosA = await new ListarProdutosQueryHandler(dbA, new FakeTenantContext(_empresaA))
                .Handle(new ListarProdutosQuery(), CancellationToken.None);

            var produto = Assert.Single(produtosA);
            Assert.Equal(produtoAId, produto.Id);
            Assert.Equal("Botijão da A", produto.Nome);
        }

        // Isolamento: a outra empresa não vê nada.
        using (var dbB = fixture.CreateDbContext(_empresaB))
        {
            var produtosB = await new ListarProdutosQueryHandler(dbB, new FakeTenantContext(_empresaB))
                .Handle(new ListarProdutosQuery(), CancellationToken.None);

            Assert.Empty(produtosB);
        }

        // Sanidade: o produto existe no banco (visão central), então a ausência acima é filtro.
        using var dbCentral = fixture.CreateDbContextCentral();
        Assert.NotNull(await dbCentral.Produtos.FindAsync([produtoAId]));
    }

    [Fact]
    public async Task Venda_em_A_nao_altera_estoque_de_produto_de_B()
    {
        // Dois produtos de mesmo nome, um em cada empresa (ids diferentes).
        Guid produtoAId;
        Guid produtoBId;

        using (var dbA = fixture.CreateDbContext(_empresaA))
        {
            produtoAId = await CriarProdutoComEstoqueAsync(dbA, _empresaA, estoque: 20);
        }

        using (var dbB = fixture.CreateDbContext(_empresaB))
        {
            produtoBId = await CriarProdutoComEstoqueAsync(dbB, _empresaB, estoque: 20);
        }

        Assert.NotEqual(produtoAId, produtoBId);

        using (var dbA = fixture.CreateDbContext(_empresaA))
        {
            await new RegistrarVendaCommandHandler(dbA, new FakeTenantContext(_empresaA))
                .Handle(new RegistrarVendaCommand(null, "Maria Souza", "11988887777", "Rua A, 1",
                    FormaPagamento.Dinheiro, 0m, [new ItemVendaInput(produtoAId, 3)]),
                    CancellationToken.None);
        }

        // Controle positivo: o estoque do produto vendido (A) baixou.
        using (var dbA = fixture.CreateDbContext(_empresaA))
        {
            var produtoA = await dbA.Produtos.SingleAsync(p => p.Id == produtoAId);
            Assert.Equal(17, produtoA.QuantidadeEstoque); // 20 - 3

            // E a venda está registrada na empresa A.
            Assert.Single(dbA.Vendas);
        }

        // Isolamento: o produto de mesmo nome da empresa B permanece intacto e B não tem vendas.
        using (var dbB = fixture.CreateDbContext(_empresaB))
        {
            var produtoB = await dbB.Produtos.SingleAsync(p => p.Id == produtoBId);
            Assert.Equal(20, produtoB.QuantidadeEstoque);

            Assert.Empty(dbB.Vendas);

            // B só tem a movimentação de entrada do seu próprio produto — nenhuma saída da venda
            // de A vazou para cá (a presença da entrada prova que a consulta enxerga dados de B).
            var movimentacoesB = await dbB.MovimentacoesEstoque.ToListAsync();
            var movimentacaoEntrada = Assert.Single(movimentacoesB);
            Assert.Equal(TipoMovimentacaoEstoque.Entrada, movimentacaoEntrada.Tipo);
            Assert.Equal(produtoBId, movimentacaoEntrada.ProdutoId);
        }

        // Sanidade: a venda existe no banco (visão central), então o "vazio" de B é filtro.
        using var dbCentral = fixture.CreateDbContextCentral();
        var vendaCentral = Assert.Single(dbCentral.Vendas);
        Assert.Equal(_empresaA, vendaCentral.EmpresaId);
    }

    [Fact]
    public async Task Emprestimo_de_vasilhame_de_A_nao_vaza_para_o_tenant_de_B()
    {
        // Cobre a nota do ObterEmprestimosClienteQueryHandler: ele não tem guarda de tenant própria
        // (a guarda fica no handler de entrada), mas a leitura é protegida pelo filtro global.
        Guid clienteAId;

        using (var dbA = fixture.CreateDbContext(_empresaA))
        {
            var tenantA = new FakeTenantContext(_empresaA);

            clienteAId = await new CadastrarClienteCommandHandler(dbA, tenantA)
                .Handle(new CadastrarClienteCommand("Cliente com Vasilhame", "11944443333", "Rua A, 1"), CancellationToken.None);

            // Produto que controla vasilhame, com 10 vazios no depósito (entrada inicial).
            var produtoAId = await new CadastrarProdutoCommandHandler(dbA, tenantA)
                .Handle(new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5,
                    ControlaVasilhame: true, QuantidadeVasilhamesInicial: 10), CancellationToken.None);

            await new RegistrarEmprestimoCommandHandler(dbA, tenantA)
                .Handle(new RegistrarEmprestimoCommand(clienteAId, produtoAId, 2, "Empréstimo de teste"), CancellationToken.None);
        }

        // Controle positivo: consultando com o tenant de A, o empréstimo aparece.
        using (var dbA = fixture.CreateDbContext(_empresaA))
        {
            var emprestimosA = await new ObterEmprestimosClienteQueryHandler(dbA)
                .Handle(new ObterEmprestimosClienteQuery(clienteAId), CancellationToken.None);

            var emprestimo = Assert.Single(emprestimosA);
            Assert.Equal(2, emprestimo.Quantidade);
            Assert.Equal("Botijão 13kg", emprestimo.ProdutoNome);
        }

        // Isolamento: consultando com o tenant de B (mesmo ClienteId), nada é retornado.
        using (var dbB = fixture.CreateDbContext(_empresaB))
        {
            var emprestimosB = await new ObterEmprestimosClienteQueryHandler(dbB)
                .Handle(new ObterEmprestimosClienteQuery(clienteAId), CancellationToken.None);

            Assert.Empty(emprestimosB);
        }

        // Sanidade: o empréstimo existe no banco (visão central).
        using var dbCentral = fixture.CreateDbContextCentral();
        Assert.Single(await dbCentral.EmprestimosVasilhames
            .Where(e => e.ClienteId == clienteAId)
            .ToListAsync());
    }

    [Fact]
    public async Task Admin_de_uma_empresa_nao_aparece_na_listagem_de_usuarios_de_outra()
    {
        // Cobre EmpresaTestHelper.CriarEmpresaEAdminAsync e a listagem de usuários (o Identity não
        // tem filtro global; o handler filtra explicitamente pela empresa do tenant).
        var email = $"admin-{Guid.NewGuid():N}@andregas.com.br";
        var (empresa, admin) = await EmpresaTestHelper.CriarEmpresaEAdminAsync(
            fixture.ConnectionString, "Empresa C Usuários", $"empresa-c-{Guid.NewGuid():N}", email, "Senha@123");

        // O helper vincula o admin à empresa criada e à role Admin.
        Assert.Equal(empresa.Id, admin.EmpresaId);
        Assert.True(admin.Ativo);

        // Controle positivo: a listagem da empresa C inclui o admin dela.
        using (var dbC = fixture.CreateDbContext(empresa.Id))
        {
            var usuariosC = await new ListarUsuariosQueryHandler(dbC, new FakeTenantContext(empresa.Id))
                .Handle(new ListarUsuariosQuery(), CancellationToken.None);

            var usuario = Assert.Single(usuariosC);
            Assert.Equal(email, usuario.Email);
            Assert.Equal("Admin", usuario.Papel);
        }

        // Isolamento: a listagem da empresa A não traz o admin da empresa C.
        using (var dbA = fixture.CreateDbContext(_empresaA))
        {
            var usuariosA = await new ListarUsuariosQueryHandler(dbA, new FakeTenantContext(_empresaA))
                .Handle(new ListarUsuariosQuery(), CancellationToken.None);

            Assert.DoesNotContain(usuariosA, u => u.Email == email);
        }

        // Sanidade: o usuário existe no banco (Identity sem filtro global).
        using var dbCentral = fixture.CreateDbContextCentral();
        Assert.True(await dbCentral.Users.AnyAsync(u => u.Email == email));
    }

    /// <summary>Cria um produto "Botijão" com <paramref name="estoque"/> unidades via handlers.</summary>
    private static async Task<Guid> CriarProdutoComEstoqueAsync(AppDbContext db, Guid empresaId, int estoque)
    {
        var tenant = new FakeTenantContext(empresaId);

        var produtoId = await new CadastrarProdutoCommandHandler(db, tenant)
            .Handle(new CadastrarProdutoCommand("Botijão", TipoProduto.Gas, 100m, 60m, 80m, 5), CancellationToken.None);

        await new RegistrarMovimentacaoEstoqueCommandHandler(db, tenant)
            .Handle(new RegistrarMovimentacaoEstoqueCommand(produtoId, TipoMovimentacaoEstoque.Entrada, estoque, "Estoque inicial"),
                CancellationToken.None);

        return produtoId;
    }
}