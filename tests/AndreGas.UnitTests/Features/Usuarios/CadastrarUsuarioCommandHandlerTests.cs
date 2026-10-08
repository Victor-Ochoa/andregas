using AndreGas.Web.Common.Authorization;
using AndreGas.Web.Features.Usuarios;
using AndreGas.Web.Features.Usuarios.Cadastrar;

namespace AndreGas.UnitTests.Features.Usuarios;

/// <summary>Fake de <see cref="IUsuarioAutenticado"/> configurável, evitando libraria de mocking.</summary>
public sealed class FakeUsuarioAutenticado(bool ehAdmin) : IUsuarioAutenticado
{
    public bool DeterminouAdmin { get; private set; }

    public Task<bool> EhAdministradorAsync(CancellationToken cancellationToken = default)
    {
        DeterminouAdmin = true;
        return Task.FromResult(ehAdmin);
    }

    public Task RequerAdminAsync(CancellationToken cancellationToken = default)
    {
        if (!ehAdmin)
        {
            throw new InvalidOperationException("Somente administradores podem executar esta operação.");
        }
        return Task.CompletedTask;
    }
}

/// <summary>Fake de <see cref="IUsuarioWriter"/> que simula o Identity sem banco.</summary>
public sealed class FakeUsuarioWriter : IUsuarioWriter
{
    public string? UltimoEmail { get; private set; }
    public string? UltimaRole { get; private set; }
    public Func<string, bool>? EmailUsadoPredicate { get; set; }
    public (Guid? UsuarioId, string? Erro) ResultadoCriacao { get; set; } = (Guid.NewGuid(), null);

    public Task<bool> EmailJaUsadoAsync(string email, CancellationToken cancellationToken = default)
    {
        UltimoEmail = email;
        return Task.FromResult(EmailUsadoPredicate?.Invoke(email) ?? false);
    }

    public Task<(Guid? UsuarioId, string? Erro)> CriarComRoleAsync(string nome, string email, string papel, string senha, CancellationToken cancellationToken = default)
    {
        UltimoEmail = email;
        UltimaRole = papel;
        return Task.FromResult(ResultadoCriacao);
    }
}

public class CadastrarUsuarioCommandHandlerTests
{
    private static CadastrarUsuarioCommand Comando() =>
        new("Maria Souza", "maria@andregas.com.br", UsuarioPapel.Vendedor, "senha123", "senha123");

    [Fact]
    public async Task Handle_DeveCriarUsuario_EAtribuirPapel_QuandoEhAdmin()
    {
        var auth = new FakeUsuarioAutenticado(ehAdmin: true);
        var writer = new FakeUsuarioWriter();
        var handler = new CadastrarUsuarioCommandHandler(auth, writer);

        var result = await handler.Handle(Comando(), CancellationToken.None);

        Assert.True(result.Sucesso);
        Assert.NotNull(result.UsuarioId);
        Assert.Equal(UsuarioPapel.Vendedor.ParaRole(), writer.UltimaRole);
        Assert.Equal("maria@andregas.com.br", writer.UltimoEmail);
    }

    [Fact]
    public async Task Handle_DeveLancar_QuandoNaoEhAdmin()
    {
        var auth = new FakeUsuarioAutenticado(ehAdmin: false);
        var writer = new FakeUsuarioWriter();
        var handler = new CadastrarUsuarioCommandHandler(auth, writer);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(Comando(), CancellationToken.None).AsTask());

        // Nenhum usuário criado (o writer não foi chamado).
        Assert.Null(writer.UltimoEmail);
    }

    [Fact]
    public async Task Handle_DeveRetornarFalha_QuandoEmailJaUsado()
    {
        var auth = new FakeUsuarioAutenticado(ehAdmin: true);
        var writer = new FakeUsuarioWriter { EmailUsadoPredicate = email => email == "maria@andregas.com.br" };
        var handler = new CadastrarUsuarioCommandHandler(auth, writer);

        var result = await handler.Handle(Comando(), CancellationToken.None);

        Assert.False(result.Sucesso);
        Assert.Contains("e-mail", result.Erro, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_DeveRetornarFalha_QuandoIdentityFalhaAoCriar()
    {
        var auth = new FakeUsuarioAutenticado(ehAdmin: true);
        var writer = new FakeUsuarioWriter { ResultadoCriacao = (null, "Falha genérica do Identity") };
        var handler = new CadastrarUsuarioCommandHandler(auth, writer);

        var result = await handler.Handle(Comando(), CancellationToken.None);

        Assert.False(result.Sucesso);
        Assert.Equal("Falha genérica do Identity", result.Erro);
    }
}