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
        using var db = InMemoryDbContextFactory.Create();

        var admin = new ApplicationUser { UserName = "admin@andregas.com.br", Email = "admin@andregas.com.br", Nome = "Admin" };
        var vendedor = new ApplicationUser { UserName = "vendedor@andregas.com.br", Email = "vendedor@andregas.com.br", Nome = "Vendedor" };
        db.Users.AddRange(admin, vendedor);
        await db.SaveChangesAsync();

        // Simula vínculo de papel (IdentityUserRole<Guid>).
        db.Set<IdentityUserRole<Guid>>().Add(new IdentityUserRole<Guid> { UserId = admin.Id, RoleId = Guid.NewGuid() });
        db.Set<IdentityUserRole<Guid>>().Add(new IdentityUserRole<Guid> { UserId = vendedor.Id, RoleId = Guid.NewGuid() });
        await db.SaveChangesAsync();

        var handler = new ListarUsuariosQueryHandler(db);
        var resultado = await handler.Handle(new ListarUsuariosQuery(), CancellationToken.None);

        Assert.Equal(2, resultado.Count);
        Assert.Contains(resultado, u => u.Email == "admin@andregas.com.br");
        Assert.Contains(resultado, u => u.Email == "vendedor@andregas.com.br");
    }
}