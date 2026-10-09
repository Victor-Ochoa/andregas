using AndreGas.Infrastructure.Identity;
using AndreGas.IntegrationTests.TestHelpers;
using AndreGas.Web.Features.Usuarios.Listar;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.IntegrationTests.Tests;

/// <summary>
/// Testes de integração da gestão de usuários contra o Postgres real (via <see cref="DatabaseFixture"/>):
/// criação de usuário com atribuição de papel usando o Identity real (via <see cref="IdentityTestHelpers"/>)
/// e listagem com o papel. Usa e-mails únicos por execução porque o Postgres do AppHost usa volume
/// persistente — <see cref="DatabaseFixture.ResetDatabaseAsync"/> não limpa a tabela de usuários do
/// Identity.
/// </summary>
public class UsuariosFeatureTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public async Task InitializeAsync() => await fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static string EmailUnico(string prefixo) => $"{prefixo}{Guid.NewGuid():N}@andregas.com.br";

    [Fact]
    public async Task CriarUsuario_DevePersistirUsuarioComPapel_NoPostgres()
    {
        var email = EmailUnico("criacao");
        var (userManager, roleManager, _) = IdentityTestHelpers.CriarManagers(fixture.ConnectionString);

        var (usuarioId, erro) = await new Web.Features.Usuarios.Cadastrar.IdentityUsuarioWriter(userManager, roleManager)
            .CriarComRoleAsync("Maria Souza", email, ApplicationRole.Vendedor, "Senha@123", CancellationToken.None);

        Assert.NotNull(usuarioId);
        Assert.Null(erro);

        using var db = fixture.CreateDbContext();
        var usuario = await db.Users.SingleAsync(u => u.Email == email);
        Assert.Equal("Maria Souza", usuario.Nome);

        var emRole = await userManager.IsInRoleAsync(usuario, ApplicationRole.Vendedor);
        Assert.True(emRole);
    }

    [Fact]
    public async Task EmailJaUsado_DeveRetornarTrue_ParaEmailExistente()
    {
        var email = EmailUnico("existente");
        var (userManager, roleManager, _) = IdentityTestHelpers.CriarManagers(fixture.ConnectionString);
        var writer = new Web.Features.Usuarios.Cadastrar.IdentityUsuarioWriter(userManager, roleManager);

        await writer.CriarComRoleAsync("Maria", email, ApplicationRole.Vendedor, "Senha@123", CancellationToken.None);

        Assert.True(await writer.EmailJaUsadoAsync(email));
        Assert.False(await writer.EmailJaUsadoAsync("naoexiste@andregas.com.br"));
    }

    [Fact]
    public async Task ListarUsuarios_DeveIncluirPapel_DoUsuarioCriado()
    {
        var email = EmailUnico("listagem");
        var (userManager, roleManager, _) = IdentityTestHelpers.CriarManagers(fixture.ConnectionString);
        await new Web.Features.Usuarios.Cadastrar.IdentityUsuarioWriter(userManager, roleManager)
            .CriarComRoleAsync("Maria Souza", email, ApplicationRole.Vendedor, "Senha@123", CancellationToken.None);

        using var db = fixture.CreateDbContext();
        var handler = new ListarUsuariosQueryHandler(db);
        var usuarios = await handler.Handle(new ListarUsuariosQuery(), CancellationToken.None);

        var maria = Assert.Single(usuarios, u => u.Email == email);
        Assert.Equal("Maria Souza", maria.Nome);
        Assert.Equal(ApplicationRole.Vendedor, maria.Papel);
    }
}