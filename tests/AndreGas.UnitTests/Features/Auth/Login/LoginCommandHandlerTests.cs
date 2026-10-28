using AndreGas.Domain.Entities;
using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Identity;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Auth.Login;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AndreGas.UnitTests.Features.Auth.Login;

/// <summary>Fake de <see cref="IPasswordSignIn"/> configurável, evitando depender de uma
/// biblioteca de mocking para simular o SignInManager do Identity.</summary>
public sealed class FakePasswordSignIn(SignInOutcome outcomeToReturn) : IPasswordSignIn
{
    public string? LastEmail { get; private set; }
    public string? LastPassword { get; private set; }
    public bool? LastRememberMe { get; private set; }

    /// <summary>Quantas vezes o sign-in foi acionado. O gate de usuário/empresa inativa deve
    /// acontecer ANTES do sign-in (0 chamadas).</summary>
    public int Chamadas { get; private set; }

    public Task<SignInOutcome> PasswordSignInAsync(string email, string password, bool rememberMe)
    {
        Chamadas++;
        LastEmail = email;
        LastPassword = password;
        LastRememberMe = rememberMe;
        return Task.FromResult(outcomeToReturn);
    }
}

/// <summary>
/// Testes do <see cref="LoginCommandHandler"/> (multi-empresa): além do resultado do sign-in, o
/// handler valida <c>usuario.Ativo</c> e <c>Empresa.Ativo</c> ANTES de autenticar e decide o
/// <see cref="LoginResult.Destino"/> (central do super admin vs. app da empresa).
/// </summary>
public class LoginCommandHandlerTests
{
    private const string Email = "usuario@demo.andregas.com.br";

    /// <summary>Senha do usuário criado no Identity (satisfaz a política padrão). O valor usado no
    /// comando é irrelevante para o handler testado — quem valida senha é o
    /// <see cref="FakePasswordSignIn"/>.</summary>
    private const string Senha = "Senha@123";

    [Fact]
    public async Task Handle_DeveRetornarSucesso_QuandoSignInSucede()
    {
        await using var cenario = await Cenario.CriarAsync(ApplicationRole.Admin, empresaAtiva: true);
        var handler = cenario.CriarHandler(SignInOutcome.Succeeded);

        var result = await handler.Handle(new LoginCommand(Email, Senha, true), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_DeveRepassarCredenciaisParaOSignIn()
    {
        await using var cenario = await Cenario.CriarAsync(ApplicationRole.Admin, empresaAtiva: true);
        var signIn = new FakePasswordSignIn(SignInOutcome.Succeeded);
        var handler = cenario.CriarHandler(signIn);

        await handler.Handle(new LoginCommand(Email, Senha, true), CancellationToken.None);

        Assert.Equal(Email, signIn.LastEmail);
        Assert.Equal(Senha, signIn.LastPassword);
        Assert.True(signIn.LastRememberMe);
    }

    [Fact]
    public async Task Handle_DeveRetornarFalha_QuandoCredenciaisInvalidas()
    {
        await using var cenario = await Cenario.CriarAsync(ApplicationRole.Admin, empresaAtiva: true);
        var handler = cenario.CriarHandler(SignInOutcome.Failed);

        var result = await handler.Handle(new LoginCommand(Email, "errada", false), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("E-mail ou senha inválidos.", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_DeveRetornarFalha_QuandoContaBloqueada()
    {
        await using var cenario = await Cenario.CriarAsync(ApplicationRole.Admin, empresaAtiva: true);
        var handler = cenario.CriarHandler(SignInOutcome.LockedOut);

        var result = await handler.Handle(new LoginCommand(Email, Senha, false), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("bloqueada", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_DeveRetornarFalha_QuandoLoginNaoPermitido()
    {
        await using var cenario = await Cenario.CriarAsync(ApplicationRole.Admin, empresaAtiva: true);
        var handler = cenario.CriarHandler(SignInOutcome.NotAllowed);

        var result = await handler.Handle(new LoginCommand(Email, Senha, false), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Login_usuario_inativo_retorna_falha_usuario_desativado()
    {
        await using var cenario = await Cenario.CriarAsync(ApplicationRole.Admin, empresaAtiva: true, usuarioAtivo: false);
        var signIn = new FakePasswordSignIn(SignInOutcome.Succeeded);
        var handler = cenario.CriarHandler(signIn);

        var result = await handler.Handle(new LoginCommand(Email, Senha, false), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("Usuário desativado. Fale com o administrador.", result.ErrorMessage);

        // O gate acontece ANTES do sign-in: nenhum cookie/autenticação é tentado.
        Assert.Equal(0, signIn.Chamadas);
    }

    [Fact]
    public async Task Login_empresa_inativa_retorna_falha_empresa_desativada()
    {
        await using var cenario = await Cenario.CriarAsync(ApplicationRole.Admin, empresaAtiva: false);
        var signIn = new FakePasswordSignIn(SignInOutcome.Succeeded);
        var handler = cenario.CriarHandler(signIn);

        var result = await handler.Handle(new LoginCommand(Email, Senha, false), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("Empresa desativada. Fale com o administrador.", result.ErrorMessage);
        Assert.Equal(0, signIn.Chamadas);
    }

    [Fact]
    public async Task Login_superadmin_sem_empresa_retorna_destino_central()
    {
        await using var cenario = await Cenario.CriarAsync(ApplicationRole.SuperAdmin, empresaAtiva: null);
        var handler = cenario.CriarHandler(SignInOutcome.Succeeded);

        var result = await handler.Handle(new LoginCommand(Email, Senha, false), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("/central", result.Destino);
    }

    [Fact]
    public async Task Login_admin_empresa_retorna_destino_home()
    {
        await using var cenario = await Cenario.CriarAsync(ApplicationRole.Admin, empresaAtiva: true);
        var handler = cenario.CriarHandler(SignInOutcome.Succeeded);

        var result = await handler.Handle(new LoginCommand(Email, Senha, false), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("/", result.Destino);
    }

    [Fact]
    public async Task Login_usuario_inexistente_segue_para_o_signin_sem_vazar_informacao()
    {
        await using var cenario = await Cenario.CriarAsync(ApplicationRole.Admin, empresaAtiva: true);
        var signIn = new FakePasswordSignIn(SignInOutcome.Failed);
        var handler = cenario.CriarHandler(signIn);

        var result = await handler.Handle(new LoginCommand("naoexiste@andregas.com.br", Senha, false), CancellationToken.None);

        // Sem usuário, o handler não bloqueia nem revela a existência: delega ao sign-in, que
        // devolve a mensagem genérica.
        Assert.False(result.Succeeded);
        Assert.Equal("E-mail ou senha inválidos.", result.ErrorMessage);
        Assert.Equal(1, signIn.Chamadas);
    }

    /// <summary>
    /// Cenário com Identity em memória fiel à produção (UserManager real) + o usuário de teste,
    /// mantendo o provider vivo até o fim do teste.
    /// </summary>
    private sealed class Cenario : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly AppDbContext _db;

        private Cenario(ServiceProvider provider, AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _provider = provider;
            _db = db;
            UserManager = userManager;
        }

        public UserManager<ApplicationUser> UserManager { get; }

        public LoginCommandHandler CriarHandler(SignInOutcome outcome) =>
            CriarHandler(new FakePasswordSignIn(outcome));

        public LoginCommandHandler CriarHandler(FakePasswordSignIn signIn) =>
            new(signIn, UserManager, _db);

        /// <param name="roleNome">Role do usuário (Admin de empresa ou SuperAdmin da central).</param>
        /// <param name="empresaAtiva">
        /// <c>true</c>/<c>false</c> cria a empresa do usuário (ativa/inativa); <c>null</c> = usuário
        /// sem empresa (central).
        /// </param>
        /// <param name="usuarioAtivo">Estado de <see cref="ApplicationUser.Ativo"/>.</param>
        public static async Task<Cenario> CriarAsync(string roleNome, bool? empresaAtiva, bool usuarioAtivo = true)
        {
            var banco = Guid.NewGuid().ToString();
            var db = InMemoryDbContextFactory.Create(nomeBanco: banco);

            Guid? empresaId = null;
            if (empresaAtiva is { } ativa)
            {
                var empresa = new Empresa("demo", "Demo Ltda");
                if (!ativa)
                {
                    empresa.Desativar();
                }

                db.Set<Empresa>().Add(empresa);
                await db.SaveChangesAsync();
                empresaId = empresa.Id;
            }

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
            var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = provider.GetRequiredService<RoleManager<ApplicationRole>>();

            await roleManager.CreateAsync(new ApplicationRole(roleNome));

            var usuario = new ApplicationUser
            {
                UserName = Email,
                Email = Email,
                EmailConfirmed = true,
                Nome = "Usuário Teste",
                EmpresaId = empresaId,
                Ativo = usuarioAtivo,
            };

            var criacao = await userManager.CreateAsync(usuario, Senha);
            Assert.True(criacao.Succeeded, string.Join("; ", criacao.Errors.Select(e => e.Description)));
            await userManager.AddToRoleAsync(usuario, roleNome);

            return new Cenario(provider, db, userManager);
        }

        public async ValueTask DisposeAsync()
        {
            await _db.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
}
