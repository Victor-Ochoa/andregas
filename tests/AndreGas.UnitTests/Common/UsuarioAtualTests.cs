using System.Security.Claims;

using AndreGas.Infrastructure.Identity;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Common;

using Microsoft.AspNetCore.Components.Authorization;

namespace AndreGas.UnitTests.Common;

/// <summary>
/// Testes de <see cref="UsuarioAtual"/>: o autor de um registro de auditoria/venda é identificado
/// pelo <c>NameIdentifier</c> do principal, não por uma reconsulta à tabela de usuários pelo e-mail.
/// </summary>
/// <remarks>
/// <b>Por que isto importa:</b> a resolução anterior fazia <c>db.Users.Where(u =&gt; u.Email == nome)</c>
/// sobre a tabela do Identity, que <b>não</b> tem filtro de empresa. Hoje isso é correto apenas
/// porque <c>RequireUniqueEmail = true</c> — uma garantia distante do ponto de uso. O
/// <c>NameIdentifier</c> é a identidade assinada do próprio usuário e não depende de o e-mail
/// continuar único/inalterado.
/// </remarks>
public class UsuarioAtualTests
{
    private const string Email = "maria@andregas.com.br";

    [Fact]
    public async Task Obter_resolve_o_id_pelo_NameIdentifier_do_principal()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);

        var usuario = new ApplicationUser
        {
            UserName = Email,
            Email = Email,
            Nome = "Maria",
            EmpresaId = TenantTeste.EmpresaId,
        };
        db.Users.Add(usuario);
        await db.SaveChangesAsync();

        var provider = new FakeAuthState(Email, usuario.Id);

        var (nome, id) = await UsuarioAtual.ObterAsync(provider, db, CancellationToken.None);

        Assert.Equal(Email, nome);
        Assert.Equal(usuario.Id, id);
    }

    /// <summary>
    /// <c>NameIdentifier</c> presente e válido vence — mesmo que o e-mail do principal não exista
    /// no banco (ex.: e-mail alterado sem novo login). Sem a mudança, a reconsulta por e-mail
    /// devolveria <c>null</c> e o registro ficaria sem autor.
    /// </summary>
    [Fact]
    public async Task Obter_usa_o_NameIdentifier_quando_o_email_nao_casa_com_nenhum_usuario()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);

        var usuario = new ApplicationUser
        {
            UserName = "antigo@andregas.com.br",
            Email = "antigo@andregas.com.br",
            Nome = "Maria",
            EmpresaId = TenantTeste.EmpresaId,
        };
        db.Users.Add(usuario);
        await db.SaveChangesAsync();

        // O principal carrega o e-mail NOVO, mas o Id correto da conta.
        var provider = new FakeAuthState("novo@andregas.com.br", usuario.Id);

        var (_, id) = await UsuarioAtual.ObterAsync(provider, db, CancellationToken.None);

        Assert.Equal(usuario.Id, id);
    }

    /// <summary>
    /// Fallback: principals sem <c>NameIdentifier</c> (vários testes de unidade e chamadas internas
    /// montam um principal só com <c>ClaimTypes.Name</c>) continuam resolvendo o Id pelo e-mail —
    /// o comportamento antigo não pode quebrar.
    /// </summary>
    [Fact]
    public async Task Obter_cai_no_email_quando_o_principal_nao_traz_NameIdentifier()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);

        var usuario = new ApplicationUser
        {
            UserName = Email,
            Email = Email,
            Nome = "Maria",
            EmpresaId = TenantTeste.EmpresaId,
        };
        db.Users.Add(usuario);
        await db.SaveChangesAsync();

        var provider = new FakeAuthState(Email, userId: null);

        var (nome, id) = await UsuarioAtual.ObterAsync(provider, db, CancellationToken.None);

        Assert.Equal(Email, nome);
        Assert.Equal(usuario.Id, id);
    }

    /// <summary>Sem autenticação: "sistema" e Id nulo (contrato inalterado).</summary>
    [Fact]
    public async Task Obter_sem_autenticacao_retorna_sistema_e_id_nulo()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);

        var (nome, id) = await UsuarioAtual.ObterAsync(
            new FakeAuthState(autenticado: false), db, CancellationToken.None);

        Assert.Equal(UsuarioAtual.Sistema, nome);
        Assert.Null(id);
    }

    /// <summary>Principal autenticado com nome e (opcionalmente) o Id da conta.</summary>
    private sealed class FakeAuthState : AuthenticationStateProvider
    {
        private readonly AuthenticationState _state;

        public FakeAuthState(string nome, Guid? userId)
        {
            var claims = new List<Claim> { new(ClaimTypes.Name, nome) };
            if (userId is { } id)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, id.ToString()));
            }

            _state = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "Fake")));
        }

        public FakeAuthState(bool autenticado)
        {
            var identity = autenticado
                ? new ClaimsIdentity([new Claim(ClaimTypes.Name, Email)], "Fake")
                : new ClaimsIdentity();
            _state = new AuthenticationState(new ClaimsPrincipal(identity));
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(_state);
    }
}