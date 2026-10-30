using AndreGas.Infrastructure.Identity;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Features.Usuarios.Listar;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.UnitTests.Features.Usuarios;

public class ListarUsuariosQueryHandlerTests
{
    [Fact]
    public async Task Handle_DeveListarUsuariosComPapel()
    {
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);

        // Usuários da empresa do tenant corrente (a listagem é escopada por empresa).
        var admin = new ApplicationUser
        {
            UserName = "admin@andregas.com.br",
            Email = "admin@andregas.com.br",
            Nome = "Admin",
            EmpresaId = TenantTeste.EmpresaId,
        };
        var vendedor = new ApplicationUser
        {
            UserName = "vendedor@andregas.com.br",
            Email = "vendedor@andregas.com.br",
            Nome = "Vendedor",
            EmpresaId = TenantTeste.EmpresaId,
        };
        db.Users.AddRange(admin, vendedor);
        await db.SaveChangesAsync();

        // Simula vínculo de papel (IdentityUserRole<Guid>).
        db.Set<IdentityUserRole<Guid>>().Add(new IdentityUserRole<Guid> { UserId = admin.Id, RoleId = Guid.NewGuid() });
        db.Set<IdentityUserRole<Guid>>().Add(new IdentityUserRole<Guid> { UserId = vendedor.Id, RoleId = Guid.NewGuid() });
        await db.SaveChangesAsync();

        var handler = new ListarUsuariosQueryHandler(db, TenantTeste.Contexto());
        var resultado = await handler.Handle(new ListarUsuariosQuery(), CancellationToken.None);

        Assert.Equal(2, resultado.Count);
        Assert.Contains(resultado, u => u.Email == "admin@andregas.com.br");
        Assert.Contains(resultado, u => u.Email == "vendedor@andregas.com.br");
    }

    /// <summary>
    /// O join <c>IdentityUserRole × ApplicationRole</c> não tem filtro de empresa (o Identity não é
    /// entidade de domínio): sem restringi-lo aos usuários da empresa, ele varre os vínculos de
    /// <b>todas</b> as empresas a cada listagem. O papel devolvido tem de ser o do próprio usuário,
    /// e o de outra empresa não pode aparecer — nem sequer ser lido.
    /// </summary>
    [Fact]
    public async Task Handle_nao_usa_papeis_de_usuarios_de_outra_empresa()
    {
        var outraEmpresa = Guid.Parse("33333333-3333-3333-3333-333333333333");
        using var db = InMemoryDbContextFactory.Create(TenantTeste.EmpresaId);

        var usuarioDaEmpresa = new ApplicationUser
        {
            UserName = "a@andregas.com.br",
            Email = "a@andregas.com.br",
            Nome = "Da Empresa",
            EmpresaId = TenantTeste.EmpresaId,
        };
        var usuarioDeOutra = new ApplicationUser
        {
            UserName = "b@andregas.com.br",
            Email = "b@andregas.com.br",
            Nome = "De Outra",
            EmpresaId = outraEmpresa,
        };
        db.Users.AddRange(usuarioDaEmpresa, usuarioDeOutra);
        await db.SaveChangesAsync();

        var roleAdmin = new ApplicationRole(ApplicationRole.Admin) { Id = Guid.NewGuid() };
        var roleVendedor = new ApplicationRole(ApplicationRole.Vendedor) { Id = Guid.NewGuid() };
        db.Set<ApplicationRole>().AddRange(roleAdmin, roleVendedor);

        // O usuário de OUTRA empresa é o Admin; o da empresa do tenant é Vendedor. Se o join não
        // for restrito, o vínculo do usuário de fora entra no dicionário (varredura cross-tenant).
        db.Set<IdentityUserRole<Guid>>().Add(new IdentityUserRole<Guid> { UserId = usuarioDaEmpresa.Id, RoleId = roleVendedor.Id });
        db.Set<IdentityUserRole<Guid>>().Add(new IdentityUserRole<Guid> { UserId = usuarioDeOutra.Id, RoleId = roleAdmin.Id });
        await db.SaveChangesAsync();

        var handler = new ListarUsuariosQueryHandler(db, TenantTeste.Contexto());
        var resultado = await handler.Handle(new ListarUsuariosQuery(), CancellationToken.None);

        var item = Assert.Single(resultado);
        Assert.Equal("a@andregas.com.br", item.Email);
        Assert.Equal(ApplicationRole.Vendedor, item.Papel);
    }
}