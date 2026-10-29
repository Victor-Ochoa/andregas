using System.Security.Claims;

using AndreGas.Domain.Entities;
using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Identity;
using AndreGas.Infrastructure.Tenancy;
using AndreGas.IntegrationTests.TestHelpers;
using AndreGas.Web.Common.Authorization;
using AndreGas.Web.Features.ConfiguracaoEmpresa;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AndreGas.IntegrationTests.Tests;

/// <summary>
/// Testes de integração da aba <b>"Minha Empresa"</b> contra o Postgres real: o administrador de uma
/// empresa edita o próprio cadastro (nome, CNPJ, endereço, telefones, cor, logo) e a edição grava
/// exatamente um <see cref="RegistroAuditoria"/> — no mesmo <c>SaveChanges</c>.
/// </summary>
/// <remarks>
/// <para>
/// O comando <b>não</b> recebe <c>EmpresaId</c>: a empresa vem do <see cref="ITenantContext"/>, e o
/// handler começa por <c>EmpresaIdRequerida</c>. No modo central (super admin sem empresa) ele lança
/// e <b>nada</b> é gravado — é o teste de recusa que garante que a central não edita empresa por
/// esta tela.
/// </para>
/// <para>
/// <b>RegistroAuditoria é global</b> (sem filtro por empresa): os asserts de auditoria usam a visão
/// central, e o teste de isolamento compara o <c>EmpresaId</c> gravado no registro, não a
/// visibilidade da linha.
/// </para>
/// </remarks>
public class ConfiguracaoEmpresaTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public async Task InitializeAsync() => await fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Admin_edita_nome_cnpj_endereco_telefones_cor_e_logo()
    {
        var (empresa, admin) = await CriarEmpresaEAdminAsync("Empresa Editar");

        var logo = "data:image/png;base64,iVBORw0KGgo=";
        await using var servicos = CriarServicos(empresa.Id, admin.Email!);

        var editado = await servicos.Handler.Handle(
            new EditarConfigEmpresaCommand(
                "André Gás e Água",
                Cnpj: "12.345.678/0001-99",
                Endereco: "Rua Nova",
                Numero: "42",
                Cep: "01001-000",
                Bairro: "Centro",
                Cidade: "São Paulo",
                Estado: "SP",
                Telefones: ["11 98888-7777", "(11) 3333-4444"],
                CorPrimaria: "#0A1F44",
                Logo: logo),
            CancellationToken.None);

        Assert.True(editado);

        using var db = fixture.CreateDbContextCentral();
        var atualizada = await db.Set<Empresa>().SingleAsync(e => e.Id == empresa.Id);

        // Controle positivo: todos os campos editáveis foram persistidos.
        Assert.Equal("André Gás e Água", atualizada.Nome);
        Assert.Equal("12.345.678/0001-99", atualizada.Cnpj);
        Assert.Equal("Rua Nova", atualizada.Endereco);
        Assert.Equal("42", atualizada.Numero);
        Assert.Equal("01001-000", atualizada.Cep);
        Assert.Equal("Centro", atualizada.Bairro);
        Assert.Equal("São Paulo", atualizada.Cidade);
        Assert.Equal("SP", atualizada.Estado);
        Assert.Equal(["11988887777", "1133334444"], atualizada.Telefones); // normalizados para dígitos
        Assert.Equal("#0A1F44", atualizada.CorPrimaria);
        Assert.Equal(logo, atualizada.Logo);

        // A empresa continua ativa: esta tela não inativa nem reativa.
        Assert.True(atualizada.Ativo);
    }

    /// <summary>
    /// A UF é normalizada para maiúsculas no handler (único ponto de escrita): "sp" no formulário
    /// tem de virar "SP" no banco, senão comparações/relatórios futuros tratam a mesma UF como duas.
    /// </summary>
    [Fact]
    public async Task Admin_edita_uf_minuscula_e_grava_em_maiusculas()
    {
        var (empresa, admin) = await CriarEmpresaEAdminAsync("Empresa Uf");

        await using var servicos = CriarServicos(empresa.Id, admin.Email!);

        await servicos.Handler.Handle(
            new EditarConfigEmpresaCommand("Empresa Uf", Estado: "sp"), CancellationToken.None);

        using var db = fixture.CreateDbContextCentral();
        Assert.Equal("SP", await db.Set<Empresa>()
            .Where(e => e.Id == empresa.Id).Select(e => e.Estado).SingleAsync());
    }

    /// <summary>
    /// Um endereço com o tamanho máximo da coluna (300) grava sem estourar; um caractere a mais é
    /// barrado pelo validador ANTES do banco (não vira <c>DbUpdateException</c> em 22001).
    /// </summary>
    [Fact]
    public async Task Admin_grava_endereco_no_limite_e_validador_barra_o_excedente()
    {
        var (empresa, admin) = await CriarEmpresaEAdminAsync("Empresa Limite");

        await using var servicos = CriarServicos(empresa.Id, admin.Email!);

        // Controle positivo: exatamente 300 chars passam na validação e são gravados.
        var noLimite = new string('a', 300);
        var valido = await servicos.Validator.ValidateAsync(
            new EditarConfigEmpresaCommand("Empresa Limite", Endereco: noLimite));
        Assert.True(valido.IsValid, string.Join(" ", valido.Errors.Select(e => e.ErrorMessage)));

        Assert.True(await servicos.Handler.Handle(
            new EditarConfigEmpresaCommand("Empresa Limite", Endereco: noLimite), CancellationToken.None));

        using var db = fixture.CreateDbContextCentral();
        Assert.Equal(noLimite, await db.Set<Empresa>()
            .Where(e => e.Id == empresa.Id).Select(e => e.Endereco).SingleAsync());

        // 301 chars: o validador recusa (e a mensagem é do campo, não um erro de banco).
        var excedente = await servicos.Validator.ValidateAsync(
            new EditarConfigEmpresaCommand("Empresa Limite", Endereco: new string('a', 301)));
        Assert.Contains(excedente.Errors, e => e.PropertyName == nameof(EditarConfigEmpresaCommand.Endereco));
    }

    [Fact]
    public async Task Admin_nao_altera_identificador()
    {
        var (empresa, admin) = await CriarEmpresaEAdminAsync("Empresa Identificador");
        var identificadorOriginal = empresa.Identificador;

        await using var servicos = CriarServicos(empresa.Id, admin.Email!);

        // O comando nem expõe o identificador (não há campo para passar): o valor no banco tem de
        // continuar exatamente o original depois de uma edição completa.
        await servicos.Handler.Handle(
            new EditarConfigEmpresaCommand(
                "Outro Nome",
                Cnpj: "12345678000199",
                Endereco: "Rua Trocada",
                Numero: "7",
                Cep: "02002000",
                Bairro: "Bairro Novo",
                Cidade: "Campinas",
                Estado: "SP",
                Telefones: ["11911112222"],
                CorPrimaria: "#FFFFFF",
                Logo: null),
            CancellationToken.None);

        using var db = fixture.CreateDbContextCentral();
        var atualizada = await db.Set<Empresa>().SingleAsync(e => e.Id == empresa.Id);

        Assert.Equal(identificadorOriginal, atualizada.Identificador);
        Assert.Equal("Outro Nome", atualizada.Nome); // controle positivo: a edição aconteceu
    }

    [Fact]
    public async Task Edicao_grava_registro_auditoria()
    {
        var (empresa, admin) = await CriarEmpresaEAdminAsync("Empresa Auditoria");

        await using var servicos = CriarServicos(empresa.Id, admin.Email!);

        await servicos.Handler.Handle(
            new EditarConfigEmpresaCommand(
                "Empresa Auditoria Renomeada",
                Cnpj: null,
                Endereco: "Rua Auditada",
                Numero: "10",
                Cep: null,
                Bairro: null,
                Cidade: "São Paulo",
                Estado: "SP",
                Telefones: ["11955556666"],
                CorPrimaria: "#123456",
                Logo: null),
            CancellationToken.None);

        using var db = fixture.CreateDbContextCentral();
        var registro = Assert.Single(await db.Set<RegistroAuditoria>().ToListAsync());

        Assert.Equal(RegistroAuditoria.Acoes.ConfigEmpresaEditada, registro.Acao);
        Assert.Equal(empresa.Id, registro.EmpresaId);
        Assert.Equal(RegistroAuditoria.Origens.App, registro.Origem);
        Assert.Equal(admin.Email, registro.UsuarioNome);
        Assert.Equal(admin.Id, registro.UsuarioId);
        Assert.False(string.IsNullOrWhiteSpace(registro.Detalhes));

        // O resumo é "antes → depois" em pt-BR: contém o valor antigo e o novo do nome.
        Assert.Contains("Nome", registro.Detalhes, StringComparison.Ordinal);
        Assert.Contains(empresa.Nome, registro.Detalhes, StringComparison.Ordinal);
        Assert.Contains("Empresa Auditoria Renomeada", registro.Detalhes, StringComparison.Ordinal);
    }

    /// <summary>
    /// O resumo "antes → depois" pode passar de 2000 chars (Logradouro 300 + Telefones ~2000 + ...).
    /// O handler tem de aplicar o corte da entidade — sem ele, a gravação estouraria a coluna e
    /// (como a escrita é atômica) derrubaria a edição.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por que não há emoji aqui:</b> o único campo longo o bastante para alcançar o corte é
    /// <c>Telefones</c>, que é normalizado para dígitos — logo um par surrogate nunca cai na
    /// fronteira dos 2000 chars por este caminho. O comportamento surrogate-safe em si é provado
    /// em <c>RegistroAuditoriaTests.Truncar_publico_recua_para_nao_partir_par_surrogate</c>.
    /// </para>
    /// <para>
    /// <b>Limite desta prova:</b> o resumo é cortado duas vezes (handler e entidade), então este
    /// teste <b>não</b> distingue qual das duas agiu — removendo só a do handler ele continua
    /// verde (verificado por mutação). O que ele garante é o comportamento observável: um resumo
    /// grande <b>não</b> estoura a coluna nem derruba a edição. A do handler existe para o corte ser
    /// surrogate-safe também ali (defesa em profundidade) e é o caminho único do corte.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Resumo_da_auditoria_acima_do_limite_e_truncado()
    {
        var (empresa, admin) = await CriarEmpresaEAdminAsync("Empresa Resumo");

        await using var servicos = CriarServicos(empresa.Id, admin.Email!);

        // 150 telefones de 11 dígitos: a coluna `Empresa.Telefones` (2000, separada por ";") fica com
        // 1799 chars e cabe; o resumo (separado por ", ") passa de 2000 e é cortado.
        await servicos.Handler.Handle(
            new EditarConfigEmpresaCommand(
                "Empresa Resumo",
                Endereco: new string('x', 300),
                Bairro: new string('b', 100),
                Cidade: new string('c', 100),
                Telefones: [.. Enumerable.Range(0, 150).Select(i => $"1199999{i:D4}")]),
            CancellationToken.None);

        using var db = fixture.CreateDbContextCentral();
        var registro = Assert.Single(await db.Set<RegistroAuditoria>().ToListAsync());

        // O resumo natural seria bem maior que a coluna; o corte foi aplicado exatamente no limite.
        Assert.Equal(RegistroAuditoria.TamanhoMaximoDetalhes, registro.Detalhes.Length);
        Assert.DoesNotContain(registro.Detalhes, char.IsSurrogate);

        // Controle positivo: o corte preserva o começo do resumo (não é um texto vazio nem o final).
        Assert.Contains("Logradouro: Rua de Teste → xxx", registro.Detalhes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Edicao_em_empresa_A_nao_cria_auditoria_em_B()
    {
        var (empresaA, adminA) = await CriarEmpresaEAdminAsync("Empresa A Auditoria");
        var (empresaB, _) = await CriarEmpresaEAdminAsync("Empresa B Auditoria");

        await using var servicos = CriarServicos(empresaA.Id, adminA.Email!);

        await servicos.Handler.Handle(
            new EditarConfigEmpresaCommand("Empresa A Renomeada"), CancellationToken.None);

        using var db = fixture.CreateDbContextCentral();
        var registros = await db.Set<RegistroAuditoria>().ToListAsync();

        // Controle positivo: exatamente um registro, da empresa A.
        var registro = Assert.Single(registros);
        Assert.Equal(empresaA.Id, registro.EmpresaId);

        // Controle negativo de tenant: nada foi atribuído à empresa B.
        Assert.DoesNotContain(registros, r => r.EmpresaId == empresaB.Id);

        // E a empresa B continua intacta.
        Assert.Equal(
            "Empresa B Auditoria",
            await db.Set<Empresa>().Where(e => e.Id == empresaB.Id).Select(e => e.Nome).SingleAsync());
    }

    /// <summary>
    /// Prova que a auditoria e a edição vão no <b>mesmo</b> <c>SaveChanges</c>: se a gravação do
    /// <see cref="RegistroAuditoria"/> falhar, a alteração da empresa também não é persistida.
    /// </summary>
    /// <remarks>
    /// Sem essa prova o teste de "edição inválida não grava auditoria" passaria vacuosamente (o
    /// domínio lança antes de qualquer gravação). Aqui a falha acontece <b>durante</b> o save, com a
    /// alteração da empresa já rastreada: dois <c>SaveChanges</c> separados deixariam o nome novo no
    /// banco e este teste falharia.
    /// </remarks>
    [Fact]
    public async Task Falha_ao_gravar_auditoria_nao_persiste_a_edicao()
    {
        var (empresa, admin) = await CriarEmpresaEAdminAsync("Empresa Atomicidade");

        await using var servicos = CriarServicos(
            empresa.Id, admin.Email!, interceptors: [new FalhaAoGravarAuditoria()]);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await servicos.Handler.Handle(
                new EditarConfigEmpresaCommand("Nome Que Não Deve Persistir"), CancellationToken.None));

        using var db = fixture.CreateDbContextCentral();
        Assert.Equal(
            "Empresa Atomicidade",
            await db.Set<Empresa>().Where(e => e.Id == empresa.Id).Select(e => e.Nome).SingleAsync());
        Assert.Empty(await db.Set<RegistroAuditoria>().ToListAsync());
    }

    [Fact]
    public async Task Edicao_sem_empresa_selecionada_lanca()
    {
        var (empresa, _) = await CriarEmpresaEAdminAsync("Empresa Central");

        // Modo central: super admin sem empresa selecionada.
        await using var servicos = CriarServicos(empresaId: null, usuario: "super@andregas.com.br");

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await servicos.Handler.Handle(
                new EditarConfigEmpresaCommand("Nome Que Não Deve Gravar"), CancellationToken.None));

        Assert.Contains("empresa", excecao.Message, StringComparison.OrdinalIgnoreCase);

        using var db = fixture.CreateDbContextCentral();

        // Nada gravado: nem auditoria, nem alteração do nome.
        Assert.Empty(await db.Set<RegistroAuditoria>().ToListAsync());
        Assert.Equal(
            "Empresa Central",
            await db.Set<Empresa>().Where(e => e.Id == empresa.Id).Select(e => e.Nome).SingleAsync());
    }

    [Fact]
    public async Task Edicao_invalida_nao_grava_auditoria_nem_altera_a_empresa()
    {
        var (empresa, admin) = await CriarEmpresaEAdminAsync("Empresa Invalida");

        await using var servicos = CriarServicos(empresa.Id, admin.Email!);

        // Nome em branco: o domínio (Empresa.AtualizarDados) lança. A auditoria é adicionada ao
        // mesmo SaveChanges da edição, então nada pode ser persistido.
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await servicos.Handler.Handle(new EditarConfigEmpresaCommand("   "), CancellationToken.None));

        using var db = fixture.CreateDbContextCentral();
        Assert.Empty(await db.Set<RegistroAuditoria>().ToListAsync());
        Assert.Equal(
            "Empresa Invalida",
            await db.Set<Empresa>().Where(e => e.Id == empresa.Id).Select(e => e.Nome).SingleAsync());
    }

    [Fact]
    public async Task Usuario_sem_papel_admin_nao_edita_nem_audita()
    {
        var (empresa, _) = await CriarEmpresaEAdminAsync("Empresa Guarda Admin");

        // Vendedor real no banco: o <c>UsuarioAutenticado</c> reforça o papel pelo UserManager
        // quando o claim não basta, então um claim "Vendedor" de um usuário admin não serviria.
        var vendedor = await CriarUsuarioAsync(empresa.Id, "Vendedor Config", ApplicationRole.Vendedor);

        await using var servicos = CriarServicos(empresa.Id, vendedor.Email!, papel: ApplicationRole.Vendedor);

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await servicos.Handler.Handle(
                new EditarConfigEmpresaCommand("Nome Do Vendedor"), CancellationToken.None));

        Assert.Contains("administradores", excecao.Message, StringComparison.OrdinalIgnoreCase);

        using var db = fixture.CreateDbContextCentral();
        Assert.Empty(await db.Set<RegistroAuditoria>().ToListAsync());
        Assert.Equal(
            "Empresa Guarda Admin",
            await db.Set<Empresa>().Where(e => e.Id == empresa.Id).Select(e => e.Nome).SingleAsync());
    }

    [Fact]
    public async Task ObterConfigEmpresa_traz_os_dados_da_empresa_do_tenant()
    {
        var (empresa, _) = await CriarEmpresaEAdminAsync("Empresa Consulta");
        var (outra, _) = await CriarEmpresaEAdminAsync("Empresa Consulta Outra");

        await using var servicos = CriarServicos(empresa.Id, "consulta@andregas.com.br");

        var detalhe = await servicos.ObterQuery.Handle(new ObterConfigEmpresaQuery(), CancellationToken.None);

        Assert.NotNull(detalhe);
        Assert.Equal(empresa.Id, detalhe.Id);
        Assert.Equal(empresa.Identificador, detalhe.Identificador);
        Assert.Equal("Empresa Consulta", detalhe.Nome);
        Assert.Equal("11999990000", Assert.Single(detalhe.Telefones)); // vem do EmpresaTestHelper
        Assert.NotEqual(outra.Id, detalhe.Id);
    }

    // ---------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Cria uma empresa real (identificador único) e um usuário ativo na role <c>Admin</c> dela.
    /// </summary>
    private async Task<(Empresa Empresa, ApplicationUser Admin)> CriarEmpresaEAdminAsync(string nome)
    {
        var identificador = $"cfg-{Guid.NewGuid():N}";
        var email = $"{identificador}@andregas.com.br";

        return await EmpresaTestHelper.CriarEmpresaEAdminAsync(
            fixture.ConnectionString, nome, identificador, email, "Senha@123");
    }

    /// <summary>Cria um usuário ativo de uma empresa com a role informada.</summary>
    private async Task<ApplicationUser> CriarUsuarioAsync(Guid empresaId, string nome, string papel)
    {
        var email = $"user-{Guid.NewGuid():N}@andregas.com.br";
        var (userManager, roleManager, provider) = IdentityTestHelpers.CriarManagers(fixture.ConnectionString);
        try
        {
            await AndreGas.Web.Features.Auth.SeedData.GarantirRolesAsync(roleManager);

            var usuario = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                Nome = nome,
                EmpresaId = empresaId,
                Ativo = true,
            };

            var criacao = await userManager.CreateAsync(usuario, "Senha@123");
            Assert.True(criacao.Succeeded, string.Join("; ", criacao.Errors.Select(e => e.Description)));

            var atribuicao = await userManager.AddToRoleAsync(usuario, papel);
            Assert.True(atribuicao.Succeeded, string.Join("; ", atribuicao.Errors.Select(e => e.Description)));

            return usuario;
        }
        finally
        {
            provider.Dispose();
        }
    }

    /// <summary>
    /// Escopo fiel à produção: <see cref="AppDbContext"/> scoped com o tenant da empresa e o
    /// interceptor de <c>EmpresaId</c>, o <see cref="UserManager{TUser}"/> real do Identity e o
    /// <see cref="IUsuarioAutenticado"/> real (que resolve o papel pelo principal).
    /// </summary>
    /// <param name="empresaId"><c>null</c> = modo central (sem empresa).</param>
    /// <param name="usuario">Nome/e-mail do principal autenticado (usado na auditoria).</param>
    /// <param name="papel">Role do principal; <c>Admin</c> por padrão.</param>
    /// <param name="interceptors">
    /// Interceptors extras (além do <see cref="SetEmpresaIdInterceptor"/>) — usado para forçar falha
    /// de gravação e provar a atomicidade edição + auditoria.
    /// </param>
    private ServicosConfigEmpresa CriarServicos(
        Guid? empresaId,
        string usuario,
        string papel = ApplicationRole.Admin,
        IInterceptor[]? interceptors = null)
    {
        var tenant = new FakeTenantContext(empresaId, ehSuperAdmin: empresaId is null);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .AddInterceptors([new SetEmpresaIdInterceptor(tenant), .. interceptors ?? []])
            .Options;

        var db = new AppDbContext(options, tenant);

        var (userManager, _, provider) = IdentityTestHelpers.CriarManagers(fixture.ConnectionString);
        var authProvider = new FakeAuthenticationStateProvider(usuario, papel);
        var usuarioAutenticado = new UsuarioAutenticado(authProvider, userManager);

        return new ServicosConfigEmpresa(db, tenant, authProvider, usuarioAutenticado, provider);
    }

    /// <summary>
    /// Interceptor que falha o <c>SaveChanges</c> quando há um <see cref="RegistroAuditoria"/> novo
    /// no change tracker. Como o handler grava edição + auditoria num único save, a falha impede a
    /// edição de persistir.
    /// </summary>
    private sealed class FalhaAoGravarAuditoria : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var temAuditoria = eventData.Context?.ChangeTracker
                .Entries<RegistroAuditoria>()
                .Any(entrada => entrada.State == EntityState.Added) ?? false;

            if (temAuditoria)
            {
                throw new InvalidOperationException("falha simulada ao gravar auditoria");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    /// <summary>Principal de teste: nome + role, autenticado.</summary>
    private sealed class FakeAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly AuthenticationState _state;

        public FakeAuthenticationStateProvider(string nome, string papel)
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, nome), new Claim(ClaimTypes.Role, papel)],
                authenticationType: "Teste");

            _state = new AuthenticationState(new ClaimsPrincipal(identity));
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(_state);
    }

    private sealed class ServicosConfigEmpresa : IAsyncDisposable
    {
        private readonly AppDbContext _db;
        private readonly ServiceProvider _provider;

        public ServicosConfigEmpresa(
            AppDbContext db,
            ITenantContext tenant,
            AuthenticationStateProvider authProvider,
            IUsuarioAutenticado usuarioAutenticado,
            ServiceProvider provider)
        {
            _db = db;
            _provider = provider;
            Handler = new EditarConfigEmpresaCommandHandler(db, tenant, usuarioAutenticado, authProvider);
            ObterQuery = new ObterConfigEmpresaQueryHandler(db, tenant);
            Validator = new EditarConfigEmpresaCommandValidator();
        }

        public EditarConfigEmpresaCommandHandler Handler { get; }

        public ObterConfigEmpresaQueryHandler ObterQuery { get; }

        public EditarConfigEmpresaCommandValidator Validator { get; }

        public async ValueTask DisposeAsync()
        {
            await _db.DisposeAsync();
            _provider.Dispose();
        }
    }
}