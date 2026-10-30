using System.Security.Claims;

using AndreGas.Domain.Entities;
using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Identity;
using AndreGas.Infrastructure.Tenancy;
using AndreGas.IntegrationTests.TestHelpers;
using AndreGas.Web.Common;
using AndreGas.Web.Common.Authorization;
using AndreGas.Web.Common.Tenancy;
using AndreGas.Web.Features.Auth;
using AndreGas.Web.Features.Central.Auditoria;
using AndreGas.Web.Features.Central.CriarEmpresa;
using AndreGas.Web.Features.Central.EditarEmpresa;
using AndreGas.Web.Features.Central.EntrarComo;
using AndreGas.Web.Features.Central.InativarEmpresa;
using AndreGas.Web.Features.Central.UsuariosEmpresa;
using AndreGas.Web.Features.Usuarios.Cadastrar;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AndreGas.IntegrationTests.Tests;

/// <summary>
/// Testes de integração da <b>auditoria da central</b> contra o Postgres real: cada comando da
/// central grava <b>exatamente um</b> <see cref="RegistroAuditoria"/> (com
/// <c>Origem = Central</c>) no mesmo commit da ação, uma ação que falha não grava nada, e a
/// consulta paginada filtra explicitamente por empresa/ação/período.
/// </summary>
/// <remarks>
/// <para>
/// <b>RegistroAuditoria é a única entidade de domínio SEM filtro global</b>: uma leitura sem
/// <c>Where(r =&gt; r.EmpresaId == ...)</c> devolve todas as empresas. Por isso os testes de filtro
/// sempre têm um <b>controle positivo cruzado</b> (a empresa B aparece quando filtrada por B) e um
/// negativo (filtrar por B não traz as linhas de A).
/// </para>
/// <para>
/// <b>Fidelidade do DI:</b> <see cref="CriarServicos"/> espelha o <c>Program.cs</c> — inclusive o
/// <c>EnrichNpgsqlDbContext&lt;AppDbContext&gt;()</c>, que liga <c>EnableRetryOnFailure()</c>. Sem
/// ele, um <c>SaveChanges</c> dentro de transação explícita lançaria em produção e não no teste.
/// </para>
/// </remarks>
public class AuditoriaTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private const string SuperAdminEmail = "super-auditoria@andregas.com.br";
    private const string SenhaPadrao = "Senha@123";

    private Guid _superAdminId;

    public async Task InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
        _superAdminId = await GarantirSuperAdminAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CriarEmpresa_grava_registro_EmpresaCriada()
    {
        await using var servicos = CriarServicos();

        var resultado = await servicos.CriarEmpresa.Handle(
            ComandoCriarEmpresa("Empresa Auditada", SlugUnico("auditada"), EmailUnico("admin")),
            CancellationToken.None);

        Assert.True(resultado.Sucesso, resultado.Erro);

        using var db = fixture.CreateDbContextCentral();
        var registro = Assert.Single(await db.RegistrosAuditoria.ToListAsync());

        Assert.Equal(RegistroAuditoria.Acoes.EmpresaCriada, registro.Acao);
        Assert.Equal(resultado.EmpresaId, registro.EmpresaId);
        Assert.Equal(RegistroAuditoria.Origens.Central, registro.Origem);
        Assert.False(string.IsNullOrWhiteSpace(registro.UsuarioNome));
        Assert.Equal(SuperAdminEmail, registro.UsuarioNome);
        Assert.Equal(_superAdminId, registro.UsuarioId);
        Assert.Contains("Empresa Auditada", registro.Detalhes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EditarEmpresa_grava_registro_EmpresaEditada_com_antes_e_depois()
    {
        await using var servicos = CriarServicos();
        var empresaId = await CriarEmpresaAsync(servicos, "Empresa Editar Auditada");

        var editado = await servicos.EditarEmpresa.Handle(
            new EditarEmpresaCommand(empresaId, "Empresa Renomeada", Cidade: "Campinas"),
            CancellationToken.None);
        Assert.True(editado);

        using var db = fixture.CreateDbContextCentral();
        var registro = Assert.Single(
            await db.RegistrosAuditoria.Where(r => r.Acao == RegistroAuditoria.Acoes.EmpresaEditada).ToListAsync());

        Assert.Equal(empresaId, registro.EmpresaId);
        Assert.Equal(RegistroAuditoria.Origens.Central, registro.Origem);
        Assert.Equal(SuperAdminEmail, registro.UsuarioNome);

        // Resumo "antes → depois" em pt-BR, com o nome antigo e o novo.
        Assert.Contains("Nome", registro.Detalhes, StringComparison.Ordinal);
        Assert.Contains("Empresa Editar Auditada", registro.Detalhes, StringComparison.Ordinal);
        Assert.Contains("Empresa Renomeada", registro.Detalhes, StringComparison.Ordinal);
        Assert.Contains("→", registro.Detalhes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InativarEmpresa_grava_registro_EmpresaInativada_e_EmpresaReativada()
    {
        await using var servicos = CriarServicos();
        var empresaId = await CriarEmpresaAsync(servicos, "Empresa Alternar Auditada");

        Assert.True(await servicos.InativarEmpresa.Handle(
            new InativarEmpresaCommand(empresaId, Ativar: false), CancellationToken.None));

        // Estado no banco depois de cada chamada: a auditoria não pode descrever um status que a
        // empresa não tem (prova que o handler realmente inativou, não só gravou um texto).
        Assert.False(await LerAtivoAsync(empresaId));

        Assert.True(await servicos.InativarEmpresa.Handle(
            new InativarEmpresaCommand(empresaId, Ativar: true), CancellationToken.None));
        Assert.True(await LerAtivoAsync(empresaId));

        using var db = fixture.CreateDbContextCentral();
        var registros = await db.RegistrosAuditoria
            .Where(r => r.EmpresaId == empresaId)
            .OrderBy(r => r.DataHora)
            .ToListAsync();

        // CriarEmpresa + inativação + reativação: uma linha por ação, nenhuma duplicada.
        Assert.Equal(3, registros.Count);
        var inativacao = Assert.Single(registros, r => r.Acao == RegistroAuditoria.Acoes.EmpresaInativada);
        var reativacao = Assert.Single(registros, r => r.Acao == RegistroAuditoria.Acoes.EmpresaReativada);

        Assert.All(registros, r => Assert.Equal(RegistroAuditoria.Origens.Central, r.Origem));

        // Cada ação amarrada ao SEU detalhe: "Inativa" contém "Ativa", então uma asserção solta
        // passaria com as ações trocadas. A direção do "antes → depois" separa as duas.
        Assert.Contains("Status: Ativa → Inativa", inativacao.Detalhes, StringComparison.Ordinal);
        Assert.Contains("Status: Inativa → Ativa", reativacao.Detalhes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CriarUsuarioEmpresa_grava_registro_UsuarioCriado()
    {
        await using var servicos = CriarServicos();
        var empresaId = await CriarEmpresaAsync(servicos, "Empresa Usuarios Auditada");
        var email = EmailUnico("vendedor");

        var criado = await servicos.CriarUsuarioEmpresa.Handle(
            new CriarUsuarioEmpresaCommand(empresaId, "Vendedor Auditado", email, ApplicationRole.Vendedor, SenhaPadrao),
            CancellationToken.None);
        Assert.NotNull(criado.UsuarioId);

        using var db = fixture.CreateDbContextCentral();
        var registro = Assert.Single(
            await db.RegistrosAuditoria.Where(r => r.Acao == RegistroAuditoria.Acoes.UsuarioCriado).ToListAsync());

        Assert.Equal(empresaId, registro.EmpresaId);
        Assert.Equal(RegistroAuditoria.Origens.Central, registro.Origem);
        Assert.Equal(SuperAdminEmail, registro.UsuarioNome);
        Assert.Contains(email, registro.Detalhes, StringComparison.Ordinal);
        Assert.Contains(ApplicationRole.Vendedor, registro.Detalhes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InativarUsuarioEmpresa_grava_registro_UsuarioDesativado_e_UsuarioReativado()
    {
        await using var servicos = CriarServicos();
        var empresaId = await CriarEmpresaAsync(servicos, "Empresa Inativar Usuario");

        var email = EmailUnico("vendedor-inativar");
        var criado = await servicos.CriarUsuarioEmpresa.Handle(
            new CriarUsuarioEmpresaCommand(empresaId, "Vendedor Inativar", email, ApplicationRole.Vendedor, SenhaPadrao),
            CancellationToken.None);
        Assert.NotNull(criado.UsuarioId);

        Assert.True((await servicos.InativarUsuarioEmpresa.Handle(
            new InativarUsuarioEmpresaCommand(empresaId, criado.UsuarioId!.Value, Ativo: false),
            CancellationToken.None)).Sucesso);

        Assert.True((await servicos.InativarUsuarioEmpresa.Handle(
            new InativarUsuarioEmpresaCommand(empresaId, criado.UsuarioId.Value, Ativo: true),
            CancellationToken.None)).Sucesso);

        using var db = fixture.CreateDbContextCentral();
        var registros = await db.RegistrosAuditoria
            .Where(r => r.EmpresaId == empresaId
                && (r.Acao == RegistroAuditoria.Acoes.UsuarioDesativado
                    || r.Acao == RegistroAuditoria.Acoes.UsuarioReativado))
            .ToListAsync();

        Assert.Equal(2, registros.Count);
        var desativacao = Assert.Single(registros, r => r.Acao == RegistroAuditoria.Acoes.UsuarioDesativado);
        var reativacao = Assert.Single(registros, r => r.Acao == RegistroAuditoria.Acoes.UsuarioReativado);

        Assert.All(registros, r =>
        {
            Assert.Equal(RegistroAuditoria.Origens.Central, r.Origem);
            Assert.Contains(email, r.Detalhes, StringComparison.Ordinal);
        });
        Assert.Contains(ApplicationRole.Vendedor, desativacao.Detalhes, StringComparison.Ordinal);
        Assert.Contains("inativado", desativacao.Detalhes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reativado", reativacao.Detalhes, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// O "Entrar como" é a exceção: o registro só é gravado <b>depois</b> do <c>SignInAsync</c>
    /// bem-sucedido, e assina o <b>super admin</b> (quem clicou) — nunca o admin impersonado.
    /// </summary>
    [Fact]
    public async Task EntrarComo_grava_registro_EntrarComo()
    {
        await using var servicos = CriarServicos();

        var (empresa, admin) = await EmpresaTestHelper.CriarEmpresaEAdminAsync(
            fixture.ConnectionString,
            "Empresa Impersonada",
            SlugUnico("impersonada"),
            EmailUnico("admin-empresa"),
            SenhaPadrao);

        var handler = servicos.CriarEntrarComoHandler(out var httpContext);

        var resultado = await handler.Handle(new EntrarComoCommand(empresa.Id), CancellationToken.None);
        Assert.True(resultado.Succeeded, resultado.ErrorMessage);

        // O cookie foi emitido (o stub registra a chamada) — a auditoria reflete o que aconteceu.
        var stub = (StubAuthenticationService)httpContext.RequestServices.GetRequiredService<IAuthenticationService>();
        Assert.Equal(1, stub.SignIns);

        using var db = fixture.CreateDbContextCentral();
        var registro = Assert.Single(await db.RegistrosAuditoria.ToListAsync());

        Assert.Equal(RegistroAuditoria.Acoes.EntrarComo, registro.Acao);
        Assert.Equal(empresa.Id, registro.EmpresaId);
        Assert.Equal(RegistroAuditoria.Origens.Central, registro.Origem);
        Assert.Equal(_superAdminId, registro.UsuarioId);
        Assert.Equal(SuperAdminEmail, registro.UsuarioNome);

        // Detalhes: e-mail do admin impersonado e o nome da empresa (não ids crus).
        Assert.Contains(admin.Email!, registro.Detalhes, StringComparison.Ordinal);
        Assert.Contains(empresa.Nome, registro.Detalhes, StringComparison.Ordinal);
    }

    /// <summary>
    /// O cookie emitido pelo "Entrar como" carrega a marca do teto ABSOLUTO de 8h
    /// (<c>ImpersonacaoIniciadaEm</c>, UTC) — sem ela a
    /// <see cref="AndreGas.Web.Common.Authorization.ValidadeAbsolutaImpersonacao"/> (fail-closed)
    /// encerraria a sessão na primeira validação, e com ela o teto passa a existir de fato.
    /// </summary>
    [Fact]
    public async Task EntrarComo_emite_a_marca_de_inicio_do_teto_absoluto()
    {
        await using var servicos = CriarServicos();

        var (empresa, _) = await EmpresaTestHelper.CriarEmpresaEAdminAsync(
            fixture.ConnectionString,
            "Empresa Teto",
            SlugUnico("teto"),
            EmailUnico("admin-teto"),
            SenhaPadrao);

        var antes = DateTimeOffset.UtcNow;
        var handler = servicos.CriarEntrarComoHandler(out var httpContext);

        var resultado = await handler.Handle(new EntrarComoCommand(empresa.Id), CancellationToken.None);
        Assert.True(resultado.Succeeded, resultado.ErrorMessage);

        var stub = (StubAuthenticationService)httpContext.RequestServices.GetRequiredService<IAuthenticationService>();
        var principal = stub.UltimoPrincipal;
        Assert.NotNull(principal);

        var valor = principal.FindFirst(TenantContext.ClaimImpersonacaoIniciadaEm)?.Value;
        Assert.False(string.IsNullOrEmpty(valor));

        var inicio = DateTimeOffset.Parse(
            valor!, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);

        // É "agora" (UTC) e é o mesmo instante usado no ExpiresUtc do ticket.
        Assert.InRange(inicio, antes.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Equal(TimeSpan.Zero, inicio.Offset);
    }

    /// <summary>
    /// A consulta filtra <b>explicitamente</b> por <c>EmpresaId</c>: a entidade é global, então sem
    /// o filtro a linha de outra empresa apareceria. O controle positivo cruzado prova que os dados
    /// das duas empresas existem e que o filtro é que separa.
    /// </summary>
    [Fact]
    public async Task ListarAuditoria_filtra_por_empresa_e_acao()
    {
        await using var servicos = CriarServicos();

        var empresaA = await CriarEmpresaAsync(servicos, "Empresa A Auditoria");
        var empresaB = await CriarEmpresaAsync(servicos, "Empresa B Auditoria");

        Assert.True(await servicos.EditarEmpresa.Handle(
            new EditarEmpresaCommand(empresaA, "Empresa A Renomeada"), CancellationToken.None));

        // Filtro por empresa + ação: só a edição de A.
        var soEdicaoDeA = await servicos.ListarAuditoria.Handle(
            new ListarAuditoriaQuery(EmpresaId: empresaA, Acao: RegistroAuditoria.Acoes.EmpresaEditada),
            CancellationToken.None);

        var item = Assert.Single(soEdicaoDeA.Itens);
        Assert.Equal(1, soEdicaoDeA.Total);
        Assert.Equal(empresaA, item.EmpresaId);
        Assert.Equal("Empresa A Renomeada", item.EmpresaNome); // nome projetado no join
        Assert.Equal(RegistroAuditoria.Acoes.EmpresaEditada, item.Acao);

        // Controle positivo: todas as ações de A (criação + edição), nenhuma de B.
        var tudoDeA = await servicos.ListarAuditoria.Handle(
            new ListarAuditoriaQuery(EmpresaId: empresaA), CancellationToken.None);
        Assert.Equal(2, tudoDeA.Total);
        Assert.All(tudoDeA.Itens, i => Assert.Equal(empresaA, i.EmpresaId));

        // Controle positivo cruzado: as linhas de B existem e aparecem quando filtradas por B.
        var tudoDeB = await servicos.ListarAuditoria.Handle(
            new ListarAuditoriaQuery(EmpresaId: empresaB), CancellationToken.None);
        Assert.Equal(1, tudoDeB.Total);
        Assert.Equal(empresaB, Assert.Single(tudoDeB.Itens).EmpresaId);

        // Controle negativo: B não tem edição (prova que o filtro de ação não "vaza" de A).
        var edicaoDeB = await servicos.ListarAuditoria.Handle(
            new ListarAuditoriaQuery(EmpresaId: empresaB, Acao: RegistroAuditoria.Acoes.EmpresaEditada),
            CancellationToken.None);
        Assert.Empty(edicaoDeB.Itens);
        Assert.Equal(0, edicaoDeB.Total);

        // Visão central (sem filtro de empresa): as três ações das duas empresas.
        var todas = await servicos.ListarAuditoria.Handle(new ListarAuditoriaQuery(), CancellationToken.None);
        Assert.Equal(3, todas.Total);

        // Mais recente primeiro.
        Assert.True(todas.Itens.Zip(todas.Itens.Skip(1)).All(par => par.First.DataHora >= par.Second.DataHora));
    }

    /// <summary>
    /// O período é interpretado em datas de Brasília (a tela mostra e filtra em horário local). O
    /// controle positivo garante que o filtro não é vacuoso: hoje encontra, amanhã não.
    /// </summary>
    [Fact]
    public async Task ListarAuditoria_filtra_por_periodo()
    {
        await using var servicos = CriarServicos();
        await CriarEmpresaAsync(servicos, "Empresa Periodo");

        var hoje = BrasilTimeZone.ParaBrasilia(DateTime.UtcNow).Date;
        var amanha = hoje.AddDays(1);

        var deHojeAteHoje = await servicos.ListarAuditoria.Handle(
            new ListarAuditoriaQuery(De: hoje, Ate: hoje), CancellationToken.None);
        Assert.Equal(1, deHojeAteHoje.Total);

        var deAmanhaEmDiante = await servicos.ListarAuditoria.Handle(
            new ListarAuditoriaQuery(De: amanha), CancellationToken.None);
        Assert.Empty(deAmanhaEmDiante.Itens);
        Assert.Equal(0, deAmanhaEmDiante.Total);

        var ateOntem = await servicos.ListarAuditoria.Handle(
            new ListarAuditoriaQuery(Ate: hoje.AddDays(-1)), CancellationToken.None);
        Assert.Equal(0, ateOntem.Total);
    }

    [Fact]
    public async Task ListarAuditoria_pagina_sem_perder_o_total()
    {
        await using var servicos = CriarServicos();
        await CriarEmpresaAsync(servicos, "Empresa Pagina A");
        await CriarEmpresaAsync(servicos, "Empresa Pagina B");
        await CriarEmpresaAsync(servicos, "Empresa Pagina C");

        var primeiraPagina = await servicos.ListarAuditoria.Handle(
            new ListarAuditoriaQuery(Pagina: 1, TamanhoPagina: 2), CancellationToken.None);

        Assert.Equal(2, primeiraPagina.Itens.Count);
        Assert.Equal(3, primeiraPagina.Total);

        var segundaPagina = await servicos.ListarAuditoria.Handle(
            new ListarAuditoriaQuery(Pagina: 2, TamanhoPagina: 2), CancellationToken.None);

        Assert.Single(segundaPagina.Itens);
        Assert.Equal(3, segundaPagina.Total);

        // Nenhuma linha repetida entre as páginas.
        Assert.Empty(primeiraPagina.Itens.Select(i => i.Id).Intersect(segundaPagina.Itens.Select(i => i.Id)));
    }

    /// <summary>
    /// Ação que falha (identificador duplicado) não pode deixar rastro: o registro entra na mesma
    /// transação e o rollback o desfaz.
    /// </summary>
    [Fact]
    public async Task Acao_que_falha_nao_grava_auditoria()
    {
        await using var servicos = CriarServicos();
        var slug = SlugUnico("duplicada");

        var primeira = await servicos.CriarEmpresa.Handle(
            ComandoCriarEmpresa("Empresa Duplicada", slug, EmailUnico("admin1")), CancellationToken.None);
        Assert.True(primeira.Sucesso, primeira.Erro);

        var comDuplicata = await servicos.CriarEmpresa.Handle(
            ComandoCriarEmpresa("Empresa Duplicada 2", slug, EmailUnico("admin2")), CancellationToken.None);
        Assert.False(comDuplicata.Sucesso);

        using var db = fixture.CreateDbContextCentral();
        var registros = await db.RegistrosAuditoria.ToListAsync();

        // Só o registro da criação bem-sucedida; a tentativa falha não deixou nenhum.
        var registro = Assert.Single(registros);
        Assert.Equal(RegistroAuditoria.Acoes.EmpresaCriada, registro.Acao);
        Assert.Equal(primeira.EmpresaId, registro.EmpresaId);
    }

    /// <summary>
    /// O caso acima curto-circuita <b>antes</b> da transação (identificador duplicado é checado
    /// antes). Este força a falha <b>depois</b> do insert da empresa: é o caminho em que o registro
    /// de auditoria já entrou no change tracker e precisa ser desfeito junto com a empresa.
    /// </summary>
    /// <remarks>
    /// O decorator de <see cref="IUsuarioWriter"/> falha em <c>CriarComRoleAsync</c>, ou seja,
    /// <b>antes</b> de o <see cref="RegistroAuditoria"/> ser adicionado — é o caminho "empresa salva,
    /// usuário não criado". O caso complementar (auditoria já <c>Added</c>/gravada) está em
    /// <see cref="Falha_no_commit_depois_da_auditoria_gravada_nao_persiste_nada"/>.
    /// </remarks>
    [Fact]
    public async Task Falha_do_identity_depois_do_insert_nao_grava_auditoria()
    {
        var emailQueFalha = EmailUnico("identity-falha");
        await using var servicos = CriarServicos(
            writer => new UsuarioWriterQueFalhaAposInsert(writer, emailQueFalha));

        var comFalha = await servicos.CriarEmpresa.Handle(
            ComandoCriarEmpresa("Empresa Auditoria Falha", SlugUnico("auditoria-falha"), emailQueFalha),
            CancellationToken.None);

        Assert.False(comFalha.Sucesso);

        // Nenhum registro fantasma no change tracker scoped (o do circuito sobrevive à falha): um
        // SaveChanges posterior NÃO pode inserir auditoria de uma ação que não aconteceu.
        Assert.DoesNotContain(
            servicos.Db.ChangeTracker.Entries<RegistroAuditoria>(),
            entrada => entrada.State == EntityState.Added);
        await servicos.Db.SaveChangesAsync();

        using var db = fixture.CreateDbContextCentral();
        Assert.Empty(await db.RegistrosAuditoria.ToListAsync());
        Assert.Empty(await db.Set<Empresa>().Where(e => e.Nome == "Empresa Auditoria Falha").ToListAsync());
    }

    /// <summary>
    /// Falha <b>depois</b> de a auditoria já ter sido gravada dentro da transação (o commit falha):
    /// o rollback tem de desfazer empresa, administrador e o registro — inclusive a linha de
    /// auditoria que já foi para o banco antes da exceção.
    /// </summary>
    [Fact]
    public async Task Falha_no_commit_depois_da_auditoria_gravada_nao_persiste_nada()
    {
        await using var servicos = CriarServicos(
            interceptors: [new FalhaAposGravarAuditoria()]);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await servicos.CriarEmpresa.Handle(
                ComandoCriarEmpresa("Empresa Commit Falha", SlugUnico("commit-falha"), EmailUnico("admin")),
                CancellationToken.None));

        using var db = fixture.CreateDbContextCentral();
        Assert.Empty(await db.RegistrosAuditoria.ToListAsync());
        Assert.Empty(await db.Set<Empresa>().Where(e => e.Nome == "Empresa Commit Falha").ToListAsync());
    }

    /// <summary>
    /// Atomicidade "ação + auditoria no mesmo <c>SaveChanges</c>" de <b>cada</b> comando que não usa
    /// transação explícita: o interceptor falha no save que carrega o registro, então a ação também
    /// não pode persistir. Um handler com dois <c>SaveChanges</c> (ação primeiro, auditoria depois)
    /// deixaria a ação no banco e este teste ficaria vermelho.
    /// </summary>
    [Fact]
    public async Task EditarEmpresa_falha_na_auditoria_nao_persiste_a_edicao()
    {
        // Setup sem o interceptor (a criação de dados de preparo também grava auditoria).
        var (empresa, _) = await EmpresaTestHelper.CriarEmpresaEAdminAsync(
            fixture.ConnectionString, "Empresa Editar Atomica", SlugUnico("editar-atomica"),
            EmailUnico("admin-editar"), SenhaPadrao);

        await using var servicos = CriarServicos(interceptors: [new FalhaAoGravarAuditoria()]);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await servicos.EditarEmpresa.Handle(
                new EditarEmpresaCommand(empresa.Id, "Nome Que Nao Deve Persistir"), CancellationToken.None));

        using var db = fixture.CreateDbContextCentral();
        Assert.Equal(
            "Empresa Editar Atomica",
            await db.Set<Empresa>().Where(e => e.Id == empresa.Id).Select(e => e.Nome).SingleAsync());
        Assert.Empty(await db.RegistrosAuditoria.ToListAsync());
    }

    [Fact]
    public async Task InativarEmpresa_falha_na_auditoria_nao_persiste_a_inativacao()
    {
        var (empresa, _) = await EmpresaTestHelper.CriarEmpresaEAdminAsync(
            fixture.ConnectionString, "Empresa Inativar Atomica", SlugUnico("inativar-atomica"),
            EmailUnico("admin-inativar"), SenhaPadrao);

        await using var servicos = CriarServicos(interceptors: [new FalhaAoGravarAuditoria()]);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await servicos.InativarEmpresa.Handle(
                new InativarEmpresaCommand(empresa.Id, Ativar: false), CancellationToken.None));

        using var db = fixture.CreateDbContextCentral();
        Assert.True(await db.Set<Empresa>().Where(e => e.Id == empresa.Id).Select(e => e.Ativo).SingleAsync());
        Assert.Empty(await db.RegistrosAuditoria.ToListAsync());
    }

    [Fact]
    public async Task CriarUsuarioEmpresa_falha_na_auditoria_nao_persiste_o_usuario()
    {
        var (empresa, _) = await EmpresaTestHelper.CriarEmpresaEAdminAsync(
            fixture.ConnectionString, "Empresa Criar Usuario Atomica", SlugUnico("criar-usuario-atomica"),
            EmailUnico("admin-criar-usuario"), SenhaPadrao);

        await using var servicos = CriarServicos(interceptors: [new FalhaAoGravarAuditoria()]);
        var email = EmailUnico("atomico");

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await servicos.CriarUsuarioEmpresa.Handle(
                new CriarUsuarioEmpresaCommand(
                    empresa.Id, "Usuario Atomico", email, ApplicationRole.Vendedor, SenhaPadrao),
                CancellationToken.None));

        using var db = fixture.CreateDbContextCentral();
        Assert.Empty(await db.Users.Where(u => u.Email == email).ToListAsync());
        Assert.Empty(await db.RegistrosAuditoria.ToListAsync());
    }

    [Fact]
    public async Task InativarUsuarioEmpresa_falha_na_auditoria_nao_persiste_a_inativacao()
    {
        var (empresa, _) = await EmpresaTestHelper.CriarEmpresaEAdminAsync(
            fixture.ConnectionString, "Empresa Inativar Usuario Atomica", SlugUnico("inativar-usuario-atomica"),
            EmailUnico("admin-inativar-usuario"), SenhaPadrao);

        // O usuário é criado com um escopo SEM o interceptor: ele precisa existir antes da ação.
        var email = EmailUnico("usuario-atomico");
        Guid usuarioId;
        await using (var preparo = CriarServicos())
        {
            var criado = await preparo.CriarUsuarioEmpresa.Handle(
                new CriarUsuarioEmpresaCommand(
                    empresa.Id, "Usuario Inativar Atomico", email, ApplicationRole.Vendedor, SenhaPadrao),
                CancellationToken.None);
            usuarioId = criado.UsuarioId!.Value;
        }

        await using var servicos = CriarServicos(interceptors: [new FalhaAoGravarAuditoria()]);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await servicos.InativarUsuarioEmpresa.Handle(
                new InativarUsuarioEmpresaCommand(empresa.Id, usuarioId, Ativo: false), CancellationToken.None));

        using var db = fixture.CreateDbContextCentral();
        Assert.True(await db.Users.Where(u => u.Id == usuarioId).Select(u => u.Ativo).SingleAsync());

        // Só a auditoria da criação do usuário (feita no escopo de preparo); nenhuma de desativação.
        Assert.DoesNotContain(
            await db.RegistrosAuditoria.ToListAsync(),
            r => r.Acao == RegistroAuditoria.Acoes.UsuarioDesativado);
    }

    /// <summary>
    /// Ordem "auditoria só depois do sign-in": se o <c>SignInAsync</c> lança, nenhum registro pode
    /// ter sido gravado. Um handler que mova o <c>Add</c>/<c>SaveChanges</c> para antes do sign-in
    /// deixa o registro no banco e este teste fica vermelho.
    /// </summary>
    [Fact]
    public async Task EntrarComo_com_sign_in_que_falha_nao_grava_auditoria()
    {
        await using var servicos = CriarServicos();

        var (empresa, _) = await EmpresaTestHelper.CriarEmpresaEAdminAsync(
            fixture.ConnectionString,
            "Empresa SignIn Falha",
            SlugUnico("signin-falha"),
            EmailUnico("admin-signin"),
            SenhaPadrao);

        var handler = servicos.CriarEntrarComoHandler(out _, new SignInQueFalha());

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.Handle(new EntrarComoCommand(empresa.Id), CancellationToken.None));

        using var db = fixture.CreateDbContextCentral();
        Assert.Empty(await db.RegistrosAuditoria.ToListAsync());
    }

    /// <summary>
    /// Guarda no servidor: a consulta da auditoria é a visão da central e só o super admin a acessa
    /// (a política da rota é apenas a primeira barreira).
    /// </summary>
    [Fact]
    public async Task ListarAuditoria_recusa_usuario_que_nao_e_super_admin()
    {
        await using var servicos = CriarServicos();

        var tenantDeEmpresa = new FakeTenantContext(TenantIntegracao.EmpresaId);

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new ListarAuditoriaQueryHandler(servicos.Db, tenantDeEmpresa)
                .Handle(new ListarAuditoriaQuery(), CancellationToken.None));

        Assert.Contains("super", excecao.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------

    private static string EmailUnico(string prefixo) => $"{prefixo}-{Guid.NewGuid():N}@andregas.com.br";

    private static string SlugUnico(string prefixo) => $"{prefixo}-{Guid.NewGuid():N}"[..Math.Min(prefixo.Length + 9, 100)];

    private static CriarEmpresaCommand ComandoCriarEmpresa(string nome, string identificador, string email) =>
        new(nome, identificador, email, SenhaPadrao, $"Admin {nome}",
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

    /// <summary>Cria uma empresa (com admin) pela central e devolve o Id.</summary>
    private static async Task<Guid> CriarEmpresaAsync(ServicosAuditoria servicos, string nome)
    {
        var resultado = await servicos.CriarEmpresa.Handle(
            ComandoCriarEmpresa(nome, SlugUnico("empresa"), EmailUnico("admin")), CancellationToken.None);

        Assert.True(resultado.Sucesso, resultado.Erro);
        return resultado.EmpresaId!.Value;
    }

    /// <summary>Cria o super admin da central (sem empresa, role <c>SuperAdmin</c>) e devolve o Id.</summary>
    private async Task<Guid> GarantirSuperAdminAsync()
    {
        var (userManager, roleManager, provider) = IdentityTestHelpers.CriarManagers(fixture.ConnectionString);
        try
        {
            await SeedData.GarantirRolesAsync(roleManager);

            var usuario = new ApplicationUser
            {
                UserName = SuperAdminEmail,
                Email = SuperAdminEmail,
                EmailConfirmed = true,
                Nome = "Super Admin Auditoria",
                EmpresaId = null,
                Ativo = true,
            };

            var criacao = await userManager.CreateAsync(usuario, SenhaPadrao);
            Assert.True(criacao.Succeeded, string.Join("; ", criacao.Errors.Select(e => e.Description)));

            var role = await userManager.AddToRoleAsync(usuario, ApplicationRole.SuperAdmin);
            Assert.True(role.Succeeded, string.Join("; ", role.Errors.Select(e => e.Description)));

            return usuario.Id;
        }
        finally
        {
            provider.Dispose();
        }
    }

    /// <summary>
    /// Escopo de serviços central fiel à produção (mesma composição do harness de
    /// <c>EmpresaGerenciamentoTests</c>), com o principal do super admin no
    /// <see cref="AuthenticationStateProvider"/> para que a auditoria assine um usuário real.
    /// </summary>
    /// <param name="decorarWriter">Decora o <see cref="IUsuarioWriter"/> real (falha simulada do Identity).</param>
    /// <param name="interceptors">
    /// Interceptors extras além do <see cref="SetEmpresaIdInterceptor"/> — usados para forçar falha
    /// de gravação e provar a atomicidade ação + auditoria. Entram no <c>AddDbContext</c> do harness,
    /// como o interceptor de tenancy entra em produção.
    /// </param>
    private ServicosAuditoria CriarServicos(
        Func<IUsuarioWriter, IUsuarioWriter>? decorarWriter = null,
        IInterceptor[]? interceptors = null)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:andregas"] = fixture.ConnectionString;
        builder.AddServiceDefaults();

        builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(fixture.ConnectionString);
            options.AddInterceptors([
                serviceProvider.GetRequiredService<SetEmpresaIdInterceptor>(),
                .. interceptors ?? [],
            ]);
        });
        builder.EnrichNpgsqlDbContext<AppDbContext>();

        builder.Services.AddScoped<SetEmpresaIdInterceptor>(_ =>
            new SetEmpresaIdInterceptor(new FakeTenantContext(empresaId: null, ehSuperAdmin: true)));

        builder.Services
            .AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        // Mesmo registro do Program.cs (o DI resolve o ÚLTIMO IUserClaimsPrincipalFactory).
        builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, ApplicationUserClaimsPrincipalFactory>();
        builder.Services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));

        var host = builder.Build();
        return new ServicosAuditoria(host, _superAdminId, decorarWriter);
    }

    /// <summary>Lê o <c>Ativo</c> da empresa pela visão central (fora do escopo do handler).</summary>
    private async Task<bool> LerAtivoAsync(Guid empresaId)
    {
        using var db = fixture.CreateDbContextCentral();
        return await db.Set<Empresa>().Where(e => e.Id == empresaId).Select(e => e.Ativo).SingleAsync();
    }

    /// <summary>Principal de teste: e-mail (nome) + Id do super admin, autenticado.</summary>
    private sealed class FakeAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly AuthenticationState _state;

        public FakeAuthenticationStateProvider(string nome, Guid usuarioId)
        {
            var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.Name, nome),
                    new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()),
                    new Claim(ClaimTypes.Role, ApplicationRole.SuperAdmin),
                    new Claim(TenantContext.ClaimEhSuperAdmin, "true"),
                ],
                authenticationType: "Teste");

            _state = new AuthenticationState(new ClaimsPrincipal(identity));
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(_state);
    }

    /// <summary>
    /// Decora o <see cref="IUsuarioWriter"/> real: para o e-mail alvo, a pré-checagem de e-mail
    /// passa e a criação devolve erro <b>sem</b> criar nada — a única forma de falhar depois do
    /// insert da empresa sem depender de regra de senha do Identity.
    /// </summary>
    private sealed class UsuarioWriterQueFalhaAposInsert(IUsuarioWriter interno, string emailQueFalha) : IUsuarioWriter
    {
        public Task<bool> EmailJaUsadoAsync(string email, CancellationToken cancellationToken = default) =>
            string.Equals(email, emailQueFalha, StringComparison.OrdinalIgnoreCase)
                ? Task.FromResult(false)
                : interno.EmailJaUsadoAsync(email, cancellationToken);

        public Task<(Guid? UsuarioId, string? Erro)> CriarComRoleAsync(
            string nome, string email, string papel, string senha, Guid? empresaId,
            CancellationToken cancellationToken = default) =>
            string.Equals(email, emailQueFalha, StringComparison.OrdinalIgnoreCase)
                ? Task.FromResult<(Guid?, string?)>((null, "falha simulada do Identity"))
                : interno.CriarComRoleAsync(nome, email, papel, senha, empresaId, cancellationToken);
    }

    /// <summary>
    /// Stub de <see cref="IAuthenticationService"/>: o <c>SignInAsync</c> do "Entrar como" precisa de
    /// um <see cref="IAuthenticationService"/> no <c>RequestServices</c>, e o serviço real exige todo
    /// o pipeline de esquemas de autenticação. Contar as chamadas prova que a auditoria só é gravada
    /// depois do sign-in bem-sucedido.
    /// </summary>
    private class StubAuthenticationService : IAuthenticationService
    {
        public int SignIns { get; private set; }

        /// <summary>Principal efetivamente emitido no último <c>SignInAsync</c> (para inspecionar
        /// as claims do cookie de impersonation).</summary>
        public ClaimsPrincipal? UltimoPrincipal { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public virtual Task SignInAsync(
            HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            SignIns++;
            UltimoPrincipal = principal;
            return Task.CompletedTask;
        }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;
    }

    /// <summary>
    /// <see cref="IAuthenticationService"/> que <b>lança</b> no <c>SignInAsync</c>: prova que o
    /// registro de auditoria só é gravado <b>depois</b> do sign-in bem-sucedido (o handler que
    /// mover o <c>SaveChanges</c> para antes do sign-in deixa o registro no banco).
    /// </summary>
    private sealed class SignInQueFalha : StubAuthenticationService
    {
        public override Task SignInAsync(
            HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) =>
            throw new InvalidOperationException("falha simulada ao emitir o cookie de impersonation");
    }

    /// <summary>Escopo de serviços em modo central com os handlers da central e o da auditoria.</summary>
    private sealed class ServicosAuditoria : IAsyncDisposable
    {
        private readonly IHost _host;
        private readonly IServiceScope _scope;
        private readonly Guid _superAdminId;

        public ServicosAuditoria(
            IHost host, Guid superAdminId, Func<IUsuarioWriter, IUsuarioWriter>? decorarWriter = null)
        {
            _host = host;
            _scope = host.Services.CreateScope();
            _superAdminId = superAdminId;

            Db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
            UserManager = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = _scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            ClaimsFactory = _scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>();

            var writerReal = new IdentityUsuarioWriter(UserManager, roleManager);
            var writer = decorarWriter is null ? writerReal : decorarWriter(writerReal);
            var tenant = new FakeTenantContext(empresaId: null, ehSuperAdmin: true);
            var authProvider = new FakeAuthenticationStateProvider(SuperAdminEmail, superAdminId);

            CriarEmpresa = new CriarEmpresaCommandHandler(Db, tenant, writer, authProvider);
            EditarEmpresa = new EditarEmpresaCommandHandler(Db, tenant, authProvider);
            InativarEmpresa = new InativarEmpresaCommandHandler(Db, tenant, authProvider);
            CriarUsuarioEmpresa = new CriarUsuarioEmpresaCommandHandler(Db, tenant, writer, authProvider);
            InativarUsuarioEmpresa = new InativarUsuarioEmpresaCommandHandler(Db, tenant, UserManager, authProvider);
            ListarAuditoria = new ListarAuditoriaQueryHandler(Db, tenant);
        }

        public AppDbContext Db { get; }

        public UserManager<ApplicationUser> UserManager { get; }

        public IUserClaimsPrincipalFactory<ApplicationUser> ClaimsFactory { get; }

        public CriarEmpresaCommandHandler CriarEmpresa { get; }

        public EditarEmpresaCommandHandler EditarEmpresa { get; }

        public InativarEmpresaCommandHandler InativarEmpresa { get; }

        public CriarUsuarioEmpresaCommandHandler CriarUsuarioEmpresa { get; }

        public InativarUsuarioEmpresaCommandHandler InativarUsuarioEmpresa { get; }

        public ListarAuditoriaQueryHandler ListarAuditoria { get; }

        /// <summary>
        /// Monta o <see cref="EntrarComoCommandHandler"/> com um <see cref="HttpContext"/> cujo
        /// principal é o super admin da central e cujo <c>RequestServices</c> tem o stub de
        /// autenticação (o handler emite o cookie via <c>SignInAsync</c>).
        /// </summary>
        public EntrarComoCommandHandler CriarEntrarComoHandler(
            out DefaultHttpContext httpContext, IAuthenticationService? authenticationService = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton(authenticationService ?? new StubAuthenticationService());
            var provider = services.BuildServiceProvider();

            httpContext = new DefaultHttpContext { RequestServices = provider };
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, _superAdminId.ToString()),
                    new Claim(ClaimTypes.Name, SuperAdminEmail),
                    new Claim(TenantContext.ClaimEhSuperAdmin, "true"),
                ],
                authenticationType: "Teste"));

            var accessor = new HttpContextAccessor { HttpContext = httpContext };

            return new EntrarComoCommandHandler(
                accessor, UserManager, Db, new PrincipalDoUsuario(ClaimsFactory), TimeProvider.System);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            _scope.Dispose();
            _host.Dispose();
        }
    }
}