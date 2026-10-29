using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Identity;
using AndreGas.IntegrationTests.TestHelpers;
using AndreGas.Web.Features.Auth.Login;
using AndreGas.Web.Features.Central.CriarEmpresa;
using AndreGas.Web.Features.Central.EditarEmpresa;
using AndreGas.Web.Features.Central.InativarEmpresa;
using AndreGas.Web.Features.Central.ListarEmpresas;
using AndreGas.Web.Features.Central.ListarProdutosEmpresa;
using AndreGas.Web.Features.Central.UsuariosEmpresa;
using AndreGas.Web.Features.Usuarios.Cadastrar;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AndreGas.IntegrationTests.Tests;

/// <summary>
/// Testes de integração da <b>central do super admin</b> contra o Postgres real (via
/// <see cref="DatabaseFixture"/>): criar/editar/inativar empresas, criar/inativar usuários de uma
/// empresa, listar produtos (somente leitura) e o reforço de super admin no servidor.
/// </summary>
/// <remarks>
/// <para>
/// Os handlers da central rodam em <b>modo central</b> (<c>ITenantContext.EmpresaId == null</c> e
/// <c>EhSuperAdmin == true</c>). A preparação/inspeção cross-tenant usa
/// <see cref="DatabaseFixture.CreateDbContextCentral"/>; a criação de dados de uma empresa usa
/// <see cref="DatabaseFixture.CreateDbContext(Guid?)"/> com o id dela (o interceptor tagueia
/// <c>EmpresaId</c> e o filtro global fecha o tenant).
/// </para>
/// <para>
/// <b>Fidelidade do DI:</b> <see cref="CriarServicos"/> monta um escopo com o mesmo
/// <c>AddIdentity</c> de produção, de modo que o <see cref="UserManager{TUser}"/> e o
/// <see cref="AppDbContext"/> do handler sejam a <b>mesma instância scoped</b> — é isso que faz a
/// transação de <c>CriarEmpresa</c> cobrir empresa + usuário.
/// </para>
/// </remarks>
public class EmpresaGerenciamentoTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public async Task InitializeAsync() => await fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CriarEmpresa_cria_empresa_e_primeiro_admin()
    {
        await using var servicos = CriarServicos();
        var email = EmailUnico("admin-empresa-nova");

        var resultado = await servicos.CriarEmpresaHandler.Handle(
            ComandoCriarEmpresa("André Gás", "andre-gas", email, "Senha@123", "Administrador André"),
            CancellationToken.None);

        Assert.True(resultado.Sucesso, resultado.Erro);
        Assert.NotNull(resultado.EmpresaId);
        Assert.NotNull(resultado.AdminId);

        using var db = fixture.CreateDbContextCentral();
        var empresa = await db.Set<Empresa>().SingleAsync(e => e.Id == resultado.EmpresaId);
        Assert.Equal("andre-gas", empresa.Identificador);
        Assert.Equal("André Gás", empresa.Nome);
        Assert.True(empresa.Ativo);

        var admin = await db.Users.SingleAsync(u => u.Id == resultado.AdminId);
        Assert.Equal(empresa.Id, admin.EmpresaId);
        Assert.True(admin.Ativo);
        Assert.True(await servicos.UserManager.IsInRoleAsync(admin, ApplicationRole.Admin));
        Assert.Equal(email, admin.Email);
    }

    [Fact]
    public async Task CriarEmpresa_com_identificador_duplicado_falha_com_mensagem()
    {
        await using var servicos = CriarServicos();

        var primeira = await servicos.CriarEmpresaHandler.Handle(
            ComandoCriarEmpresa("André Gás", "andre-gas", EmailUnico("admin1"), "Senha@123", "Admin 1"),
            CancellationToken.None);
        Assert.True(primeira.Sucesso, primeira.Erro);

        var comandoDuplicado = ComandoCriarEmpresa(
            "Outra Gás", "andre-gas", EmailUnico("admin2"), "Senha@123", "Admin 2");

        // O validador (usado pelo pipeline do Mediator na UI) já barra com mensagem pt-BR...
        var validacao = await new CriarEmpresaCommandValidator(servicos.Db).ValidateAsync(comandoDuplicado);
        Assert.False(validacao.IsValid);
        Assert.Contains(validacao.Errors, e =>
            e.PropertyName == nameof(CriarEmpresaCommand.Identificador)
            && e.ErrorMessage.Contains("identificador", StringComparison.OrdinalIgnoreCase));

        // ...e o handler também devolve erro (defesa em profundidade contra corrida), sem criar
        // uma segunda empresa nem um usuário órfão.
        var segunda = await servicos.CriarEmpresaHandler.Handle(comandoDuplicado, CancellationToken.None);

        Assert.False(segunda.Sucesso);
        Assert.Null(segunda.EmpresaId);
        Assert.False(string.IsNullOrWhiteSpace(segunda.Erro));

        using var db = fixture.CreateDbContextCentral();
        Assert.Equal(1, await db.Set<Empresa>().CountAsync(e => e.Identificador == "andre-gas"));
        Assert.Equal(1, await db.Users.CountAsync(u => u.EmpresaId != null));
    }

    [Fact]
    public async Task CriarEmpresa_com_usuario_duplicado_nao_deixa_empresa_orfa()
    {
        await using var servicos = CriarServicos();
        var emailJaUsado = EmailUnico("repetido");

        var primeira = await servicos.CriarEmpresaHandler.Handle(
            ComandoCriarEmpresa("Primeira Gás", "primeira-gas", emailJaUsado, "Senha@123", "Admin Primeira"),
            CancellationToken.None);
        Assert.True(primeira.Sucesso, primeira.Erro);

        // A falha do Identity (e-mail já usado) acontece DEPOIS de a empresa ter sido salva: sem
        // transação, a empresa ficaria órfã (sem nenhum administrador).
        var segunda = await servicos.CriarEmpresaHandler.Handle(
            ComandoCriarEmpresa("Segunda Gás", "segunda-gas", emailJaUsado, "Senha@123", "Admin Segunda"),
            CancellationToken.None);

        Assert.False(segunda.Sucesso);
        Assert.False(string.IsNullOrWhiteSpace(segunda.Erro));

        using var db = fixture.CreateDbContextCentral();
        Assert.False(await db.Set<Empresa>().AnyAsync(e => e.Identificador == "segunda-gas"));

        // Controle positivo: a primeira empresa continua íntegra (o rollback não derrubou nada dela).
        var primeiraEmpresa = await db.Set<Empresa>().SingleAsync(e => e.Identificador == "primeira-gas");
        Assert.True(await db.Users.AnyAsync(u => u.EmpresaId == primeiraEmpresa.Id));
    }

    [Fact]
    public async Task EditarEmpresa_altera_nome_cnpj_endereco_cor_logo_e_preserva_identificador()
    {
        await using var servicos = CriarServicos();

        var criada = await servicos.CriarEmpresaHandler.Handle(
            ComandoCriarEmpresa("André Gás", "andre-gas", EmailUnico("admin-editar"), "Senha@123", "Admin Editar"),
            CancellationToken.None);
        Assert.True(criada.Sucesso, criada.Erro);
        var empresaId = criada.EmpresaId!.Value;

        var logo = "data:image/png;base64,iVBORw0KGgo=";
        var editado = await servicos.EditarEmpresaHandler.Handle(
            new EditarEmpresaCommand(
                empresaId,
                "André Gás e Água",
                Cnpj: "12.345.678/0001-99",
                Endereco: "Rua Nova",
                Numero: "42",
                Cep: "01001-000",
                Bairro: "Centro",
                Cidade: "São Paulo",
                Estado: "SP",
                Telefones: ["11 98888-7777"],
                CorPrimaria: "#0A1F44",
                Logo: logo),
            CancellationToken.None);

        Assert.True(editado);

        using var db = fixture.CreateDbContextCentral();
        var empresa = await db.Set<Empresa>().SingleAsync(e => e.Id == empresaId);
        Assert.Equal("André Gás e Água", empresa.Nome);
        Assert.Equal("12.345.678/0001-99", empresa.Cnpj);
        Assert.Equal("Rua Nova", empresa.Endereco);
        Assert.Equal("42", empresa.Numero);
        Assert.Equal("01001-000", empresa.Cep);
        Assert.Equal("Centro", empresa.Bairro);
        Assert.Equal("São Paulo", empresa.Cidade);
        Assert.Equal("SP", empresa.Estado);
        Assert.Equal("11988887777", Assert.Single(empresa.Telefones)); // normalizado para dígitos
        Assert.Equal("#0A1F44", empresa.CorPrimaria);
        Assert.Equal(logo, empresa.Logo);

        // Identificador é imutável: o comando nem o carrega e o domínio não o altera.
        Assert.Equal("andre-gas", empresa.Identificador);
    }

    [Fact]
    public async Task InativarEmpresa_impede_login_do_admin()
    {
        await using var servicos = CriarServicos();
        var email = EmailUnico("admin-inativar");

        var criada = await servicos.CriarEmpresaHandler.Handle(
            ComandoCriarEmpresa("Gás Inativada", "gas-inativada", email, "Senha@123", "Admin Inativar"),
            CancellationToken.None);
        Assert.True(criada.Sucesso, criada.Erro);
        var empresaId = criada.EmpresaId!.Value;

        // Controle positivo: antes de inativar, o login do admin passa pelo gate de empresa.
        var signIn = new FakePasswordSignIn();
        var login = new LoginCommandHandler(signIn, servicos.UserManager, servicos.Db);
        var antes = await login.Handle(new LoginCommand(email, "Senha@123", false), CancellationToken.None);
        Assert.True(antes.Succeeded, antes.ErrorMessage);
        Assert.Equal(1, signIn.Chamadas);

        var inativada = await servicos.InativarEmpresaHandler.Handle(
            new InativarEmpresaCommand(empresaId, Ativar: false), CancellationToken.None);
        Assert.True(inativada);

        var depois = await login.Handle(new LoginCommand(email, "Senha@123", false), CancellationToken.None);

        Assert.False(depois.Succeeded);
        Assert.Contains("Empresa desativada", depois.ErrorMessage!, StringComparison.Ordinal);
        Assert.Equal(1, signIn.Chamadas); // o gate roda ANTES do sign-in

        // Reativar devolve o acesso (não inativamos usuários em cascata).
        Assert.True(await servicos.InativarEmpresaHandler.Handle(
            new InativarEmpresaCommand(empresaId, Ativar: true), CancellationToken.None));

        var reativada = await login.Handle(new LoginCommand(email, "Senha@123", false), CancellationToken.None);
        Assert.True(reativada.Succeeded, reativada.ErrorMessage);
    }

    [Fact]
    public async Task ListarProdutosEmpresa_retorna_somente_produtos_da_empresa_alvo()
    {
        await using var servicos = CriarServicos();

        var empresaA = await CriarEmpresaComAdminAsync(servicos, "Empresa A", "empresa-a-produtos");
        var empresaB = await CriarEmpresaComAdminAsync(servicos, "Empresa B", "empresa-b-produtos");

        using (var dbA = fixture.CreateDbContext(empresaA))
        {
            dbA.Produtos.Add(new Produto("Botijão 13kg A", TipoProduto.Gas, 100m, 60m, 80m));
            await dbA.SaveChangesAsync();
        }

        using (var dbB = fixture.CreateDbContext(empresaB))
        {
            dbB.Produtos.Add(new Produto("Água 20L B", TipoProduto.Agua, 10m, 5m, 8m));
            await dbB.SaveChangesAsync();
        }

        var produtosDeA = await servicos.ListarProdutosEmpresaHandler.Handle(
            new ListarProdutosEmpresaQuery(empresaA), CancellationToken.None);

        var produtoA = Assert.Single(produtosDeA);
        Assert.Equal("Botijão 13kg A", produtoA.Nome);

        // Controle positivo cruzado: a query da empresa B não devolve o produto de A.
        var produtosDeB = await servicos.ListarProdutosEmpresaHandler.Handle(
            new ListarProdutosEmpresaQuery(empresaB), CancellationToken.None);
        var produtoB = Assert.Single(produtosDeB);
        Assert.Equal("Água 20L B", produtoB.Nome);
    }

    [Fact]
    public async Task CriarUsuarioEmpresa_cria_usuario_na_empresa_alvo()
    {
        await using var servicos = CriarServicos();

        var empresaA = await CriarEmpresaComAdminAsync(servicos, "Empresa A", "empresa-a-usuarios");
        var empresaB = await CriarEmpresaComAdminAsync(servicos, "Empresa B", "empresa-b-usuarios");
        var email = EmailUnico("vendedor-a");

        var criado = await servicos.CriarUsuarioEmpresaHandler.Handle(
            new CriarUsuarioEmpresaCommand(empresaA, "Vendedor A", email, ApplicationRole.Vendedor, "Senha@123"),
            CancellationToken.None);

        Assert.NotNull(criado.UsuarioId);
        Assert.Null(criado.Erro);

        var usuariosDeA = await servicos.ListarUsuariosEmpresaHandler.Handle(
            new ListarUsuariosEmpresaQuery(empresaA), CancellationToken.None);
        var novo = Assert.Single(usuariosDeA, u => u.Email == email);
        Assert.Equal("Vendedor A", novo.Nome);
        Assert.Equal(ApplicationRole.Vendedor, novo.Papel);
        Assert.True(novo.Ativo);

        // Não aparece na listagem da empresa B.
        var usuariosDeB = await servicos.ListarUsuariosEmpresaHandler.Handle(
            new ListarUsuariosEmpresaQuery(empresaB), CancellationToken.None);
        Assert.DoesNotContain(usuariosDeB, u => u.Email == email);
    }

    [Fact]
    public async Task ListarEmpresas_filtra_por_busca_e_conta_usuarios_por_empresa()
    {
        await using var servicos = CriarServicos();

        var empresaA = await CriarEmpresaComAdminAsync(servicos, "Alfa Gás", "alfa-gas");
        var empresaB = await CriarEmpresaComAdminAsync(servicos, "Beta Água", "beta-agua");

        // Um segundo usuário só em Alfa: a contagem tem de ser por empresa (sem N+1 e sem somar).
        await servicos.CriarUsuarioEmpresaHandler.Handle(
            new CriarUsuarioEmpresaCommand(empresaA, "Segundo da Alfa", EmailUnico("segundo-alfa"), ApplicationRole.Vendedor, "Senha@123"),
            CancellationToken.None);

        var todas = await servicos.ListarEmpresasHandler.Handle(
            new ListarEmpresasQuery(Busca: null, Ativo: null), CancellationToken.None);

        var alfa = Assert.Single(todas, e => e.Id == empresaA);
        var beta = Assert.Single(todas, e => e.Id == empresaB);
        Assert.Equal(2, alfa.TotalUsuarios);
        Assert.Equal(1, beta.TotalUsuarios);
        Assert.True(alfa.Ativo);
        Assert.False(alfa.TemLogo);
        Assert.Equal("SP", alfa.Estado);

        // Busca por nome: só Alfa.
        var filtradas = await servicos.ListarEmpresasHandler.Handle(
            new ListarEmpresasQuery("Alfa", Ativo: null), CancellationToken.None);
        Assert.Equal(empresaA, Assert.Single(filtradas).Id);
    }

    [Fact]
    public async Task Handlers_da_central_recusam_usuario_que_nao_e_super_admin()
    {
        await using var servicos = CriarServicos();
        var empresaId = await CriarEmpresaComAdminAsync(servicos, "Empresa Comum", "empresa-comum-guarda");

        // Tenant de um administrador de empresa (não é super admin da central).
        var tenantDeEmpresa = new FakeTenantContext(empresaId);

        var excecaoListar = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new ListarEmpresasQueryHandler(servicos.Db, tenantDeEmpresa)
                .Handle(new ListarEmpresasQuery(null, null), CancellationToken.None));
        Assert.Contains("super", excecaoListar.Message, StringComparison.OrdinalIgnoreCase);

        var excecaoCriar = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new CriarEmpresaCommandHandler(servicos.Db, tenantDeEmpresa, servicos.UsuarioWriter)
                .Handle(ComandoCriarEmpresa("Inválida", "invalida", EmailUnico("x"), "Senha@123", "X"),
                    CancellationToken.None));
        Assert.Contains("super", excecaoCriar.Message, StringComparison.OrdinalIgnoreCase);

        var excecaoProdutos = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new ListarProdutosEmpresaQueryHandler(servicos.Db, tenantDeEmpresa)
                .Handle(new ListarProdutosEmpresaQuery(empresaId), CancellationToken.None));
        Assert.Contains("super", excecaoProdutos.Message, StringComparison.OrdinalIgnoreCase);

        // Nada foi criado pelo comando negado.
        using var db = fixture.CreateDbContextCentral();
        Assert.False(await db.Set<Empresa>().AnyAsync(e => e.Identificador == "invalida"));
    }

    [Fact]
    public async Task InativarUsuarioEmpresa_recusa_inativar_ultimo_admin_ativo()
    {
        await using var servicos = CriarServicos();

        var empresaId = await CriarEmpresaComAdminAsync(servicos, "Empresa Um Admin", "empresa-um-admin");
        var usuarios = await servicos.ListarUsuariosEmpresaHandler.Handle(
            new ListarUsuariosEmpresaQuery(empresaId), CancellationToken.None);
        var admin = Assert.Single(usuarios, u => u.Papel == ApplicationRole.Admin);

        var recusado = await servicos.InativarUsuarioEmpresaHandler.Handle(
            new InativarUsuarioEmpresaCommand(empresaId, admin.Id, Ativo: false), CancellationToken.None);

        Assert.False(recusado.Sucesso);
        Assert.Contains("administrador", recusado.Erro!, StringComparison.OrdinalIgnoreCase);

        // Controle positivo: com um segundo admin ativo, inativar o primeiro é permitido.
        var segundo = await servicos.CriarUsuarioEmpresaHandler.Handle(
            new CriarUsuarioEmpresaCommand(empresaId, "Segundo Admin", EmailUnico("segundo-admin"), ApplicationRole.Admin, "Senha@123"),
            CancellationToken.None);
        Assert.NotNull(segundo.UsuarioId);

        var permitido = await servicos.InativarUsuarioEmpresaHandler.Handle(
            new InativarUsuarioEmpresaCommand(empresaId, admin.Id, Ativo: false), CancellationToken.None);
        Assert.True(permitido.Sucesso, permitido.Erro);

        using var db = fixture.CreateDbContextCentral();
        Assert.False(await db.Users.Where(u => u.Id == admin.Id).Select(u => u.Ativo).SingleAsync());
    }

    [Fact]
    public async Task InativarUsuarioEmpresa_recusa_usuario_de_outra_empresa()
    {
        await using var servicos = CriarServicos();

        var empresaA = await CriarEmpresaComAdminAsync(servicos, "Empresa A", "empresa-a-guarda");
        var empresaB = await CriarEmpresaComAdminAsync(servicos, "Empresa B", "empresa-b-guarda");

        var usuariosDeB = await servicos.ListarUsuariosEmpresaHandler.Handle(
            new ListarUsuariosEmpresaQuery(empresaB), CancellationToken.None);
        var adminDeB = Assert.Single(usuariosDeB, u => u.Papel == ApplicationRole.Admin);

        // Usuário da empresa B passado com o id da empresa A: tem de ser recusado (nunca aceite
        // um id de usuário de outra empresa).
        var resultado = await servicos.InativarUsuarioEmpresaHandler.Handle(
            new InativarUsuarioEmpresaCommand(empresaA, adminDeB.Id, Ativo: false), CancellationToken.None);

        Assert.False(resultado.Sucesso);
        Assert.Contains("não encontrado", resultado.Erro!, StringComparison.OrdinalIgnoreCase);

        using var db = fixture.CreateDbContextCentral();
        Assert.True(await db.Users.Where(u => u.Id == adminDeB.Id).Select(u => u.Ativo).SingleAsync());
    }

    // ---------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------

    private static string EmailUnico(string prefixo) => $"{prefixo}-{Guid.NewGuid():N}@andregas.com.br";

    private static CriarEmpresaCommand ComandoCriarEmpresa(
        string nome, string identificador, string email, string senha, string nomeAdmin) =>
        new(nome, identificador, email, senha, nomeAdmin,
            Cnpj: null,
            Endereco: "Rua de Teste",
            Numero: "100",
            Cep: "01001000",
            Bairro: "Centro",
            Cidade: "São Paulo",
            Estado: "SP",
            Telefones: ["11999990000"],
            CorPrimaria: null,
            Logo: null);

    /// <summary>
    /// Cria uma empresa ativa com um admin ativo (identificador único por chamada) e devolve o Id.
    /// </summary>
    private async Task<Guid> CriarEmpresaComAdminAsync(ServicosCentral servicos, string nome, string identificador)
    {
        // O Postgres do AppHost tem volume persistente: sufixo aleatório no slug (índice único)
        // para nunca colidir com uma execução anterior.
        var slug = $"{identificador}-{Guid.NewGuid():N}"[..Math.Min(identificador.Length + 9, 100)];

        var resultado = await servicos.CriarEmpresaHandler.Handle(
            ComandoCriarEmpresa(nome, slug, EmailUnico("admin"), "Senha@123", $"Admin {nome}"),
            CancellationToken.None);

        Assert.True(resultado.Sucesso, resultado.Erro);
        return resultado.EmpresaId!.Value;
    }

    /// <summary>
    /// Escopo de serviços central fiel à produção: <c>UserManager</c> e <c>AppDbContext</c> são a
    /// <b>mesma instância scoped</b>, então a transação de <c>CriarEmpresa</c> cobre empresa e
    /// usuário (como no app web).
    /// </summary>
    private ServicosCentral CriarServicos()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(fixture.ConnectionString));

        services
            .AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        var provider = services.BuildServiceProvider();
        return new ServicosCentral(provider);
    }

    /// <summary>Fake de <see cref="IPasswordSignIn"/> que sempre autentica e conta as chamadas.</summary>
    private sealed class FakePasswordSignIn : IPasswordSignIn
    {
        public int Chamadas { get; private set; }

        public Task<SignInOutcome> PasswordSignInAsync(string email, string password, bool rememberMe)
        {
            Chamadas++;
            return Task.FromResult(SignInOutcome.Succeeded);
        }
    }

    /// <summary>Escopo de serviços em <b>modo central</b> (super admin), com os handlers da central.</summary>
    private sealed class ServicosCentral : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;

        public ServicosCentral(ServiceProvider provider)
        {
            _provider = provider;
            _scope = provider.CreateScope();

            Db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
            UserManager = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = _scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            UsuarioWriter = new IdentityUsuarioWriter(UserManager, roleManager);

            var tenant = new FakeTenantContext(empresaId: null, ehSuperAdmin: true);
            CriarEmpresaHandler = new CriarEmpresaCommandHandler(Db, tenant, UsuarioWriter);
            EditarEmpresaHandler = new EditarEmpresaCommandHandler(Db, tenant);
            InativarEmpresaHandler = new InativarEmpresaCommandHandler(Db, tenant);
            ListarEmpresasHandler = new ListarEmpresasQueryHandler(Db, tenant);
            ListarProdutosEmpresaHandler = new ListarProdutosEmpresaQueryHandler(Db, tenant);
            ListarUsuariosEmpresaHandler = new ListarUsuariosEmpresaQueryHandler(Db, tenant);
            CriarUsuarioEmpresaHandler = new CriarUsuarioEmpresaCommandHandler(Db, tenant, UsuarioWriter);
            InativarUsuarioEmpresaHandler = new InativarUsuarioEmpresaCommandHandler(Db, tenant);
        }

        public AppDbContext Db { get; }

        public UserManager<ApplicationUser> UserManager { get; }

        public IUsuarioWriter UsuarioWriter { get; }

        public CriarEmpresaCommandHandler CriarEmpresaHandler { get; }

        public EditarEmpresaCommandHandler EditarEmpresaHandler { get; }

        public InativarEmpresaCommandHandler InativarEmpresaHandler { get; }

        public ListarEmpresasQueryHandler ListarEmpresasHandler { get; }

        public ListarProdutosEmpresaQueryHandler ListarProdutosEmpresaHandler { get; }

        public ListarUsuariosEmpresaQueryHandler ListarUsuariosEmpresaHandler { get; }

        public CriarUsuarioEmpresaCommandHandler CriarUsuarioEmpresaHandler { get; }

        public InativarUsuarioEmpresaCommandHandler InativarUsuarioEmpresaHandler { get; }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            _scope.Dispose();
            await _provider.DisposeAsync();
        }
    }
}
/// <summary>
/// Testes HTTP do "Entrar como" a partir da central: a recusa de negócio (empresa inativa / sem
/// administrador ativo) precisa ser <b>visível</b> na tela, e não um 403 numa página de erro.
/// </summary>
/// <remarks>
/// Usa <see cref="WebAppFixture"/> porque o caminho testado é o endpoint POST
/// <c>/central/entrar-como</c> (troca de cookie + redirect), não um handler isolado.
/// </remarks>
public class CentralEntrarComoFalhaVisivelTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>, IAsyncLifetime
{
    private const string SuperAdminEmail = AndreGas.Web.Features.Auth.SeedData.DefaultEmail;
    private const string SuperAdminPassword = AndreGas.Web.Features.Auth.SeedData.DefaultPassword;

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Entrar_como_empresa_inativa_volta_a_central_com_mensagem()
    {
        var empresaId = await CriarEmpresaAsync("Empresa Inativa", ativo: false);

        using var client = fixture.CreateClient();
        await AutenticarComoSuperAdminAsync(client);

        var token = await ObterTokenAntiforgeryAsync(client);
        using var resposta = await PostEntrarComoAsync(client, token, empresaId);

        // Antes: 403 (página de erro, sem explicação). Agora: volta para a central com o motivo.
        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Contains("/central", resposta.Headers.Location!.OriginalString, StringComparison.Ordinal);
        Assert.Contains("erro=empresa-inativa", resposta.Headers.Location!.OriginalString, StringComparison.Ordinal);

        // A central renderiza a mensagem e o principal NÃO foi trocado (continua super admin).
        using var central = await client.GetAsync("/central?erro=empresa-inativa");
        Assert.Equal(HttpStatusCode.OK, central.StatusCode);
        var html = await central.Content.ReadAsStringAsync();
        Assert.Contains("Empresa desativada", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Entrando como", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Entrar_como_empresa_sem_admin_ativo_volta_a_central_com_mensagem()
    {
        var empresaId = await CriarEmpresaAsync("Empresa Sem Admin", ativo: true);

        using var client = fixture.CreateClient();
        await AutenticarComoSuperAdminAsync(client);

        var token = await ObterTokenAntiforgeryAsync(client);
        using var resposta = await PostEntrarComoAsync(client, token, empresaId);

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Contains("erro=sem-admin", resposta.Headers.Location!.OriginalString, StringComparison.Ordinal);

        using var central = await client.GetAsync("/central?erro=sem-admin");
        var html = await central.Content.ReadAsStringAsync();
        Assert.Contains("administrador ativo", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Central_lista_as_empresas_e_mostra_o_botao_entrar_como()
    {
        await CriarEmpresaAsync("Empresa Visivel", ativo: true);

        using var client = fixture.CreateClient();
        await AutenticarComoSuperAdminAsync(client);

        using var central = await client.GetAsync("/central");
        Assert.Equal(HttpStatusCode.OK, central.StatusCode);

        var html = await central.Content.ReadAsStringAsync();

        // Controle positivo da tela: título, empresa listada e o form POST do "Entrar como"
        // (o mesmo endpoint da Task 9, com antiforgery e o id da empresa no corpo).
        Assert.Contains("Central", html, StringComparison.Ordinal);
        // Nome sem acento de propósito: o HTML serializa "í" como entidade (&#xED;) e a asserção
        // ficaria dependente do encoding, não do comportamento.
        Assert.Contains("Empresa Visivel", html, StringComparison.Ordinal);
        Assert.Contains("action=\"/central/entrar-como\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"empresaId\"", html, StringComparison.Ordinal);
    }

    /// <summary>Cria uma empresa real (sem administrador) no Postgres compartilhado.</summary>
    private async Task<Guid> CriarEmpresaAsync(string nome, bool ativo)
    {
        using var db = fixture.CreateDbContext();
        var empresa = new Empresa($"visivel-{Guid.NewGuid():N}", nome);
        if (!ativo)
        {
            empresa.Desativar();
        }

        db.Set<Empresa>().Add(empresa);
        await db.SaveChangesAsync();
        return empresa.Id;
    }

    private static async Task AutenticarComoSuperAdminAsync(HttpClient client)
    {
        using var loginPage = await client.GetAsync("/login");
        var html = await loginPage.Content.ReadAsStringAsync();

        var token = System.Text.RegularExpressions.Regex
            .Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"").Groups[1].Value;
        var handler = System.Text.RegularExpressions.Regex
            .Match(html, "name=\"_handler\" value=\"([^\"]*)\"").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token));
        Assert.False(string.IsNullOrEmpty(handler));

        var form = new Dictionary<string, string>
        {
            ["_handler"] = handler,
            ["__RequestVerificationToken"] = token,
            ["Input.Email"] = SuperAdminEmail,
            ["Input.Password"] = SuperAdminPassword,
            ["Input.RememberMe"] = "true",
        };

        using var resposta = await client.PostAsync("/login", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Contains("/central", resposta.Headers.Location!.OriginalString, StringComparison.Ordinal);
    }

    /// <summary>
    /// Token antiforgery lido de <c>/login</c> (SSR estático). O cookie antiforgery é o mesmo da
    /// sessão, então o token vale para os endpoints POST do app autenticado.
    /// </summary>
    private static async Task<string> ObterTokenAntiforgeryAsync(HttpClient client)
    {
        using var resposta = await client.GetAsync("/login");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var html = await resposta.Content.ReadAsStringAsync();
        var token = System.Text.RegularExpressions.Regex
            .Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"").Groups[1].Value;

        Assert.False(string.IsNullOrEmpty(token));
        return token;
    }

    private static Task<HttpResponseMessage> PostEntrarComoAsync(HttpClient client, string token, Guid empresaId) =>
        client.PostAsync("/central/entrar-como", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["empresaId"] = empresaId.ToString(),
            ["__RequestVerificationToken"] = token,
        }));
}
