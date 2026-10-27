using AndreGas.Domain.Entities;

namespace AndreGas.UnitTests.Domain;

public class RecorrenciaCompraTests
{
    private static readonly DateTime DataBase = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Ctor_DeveGuardarEstado()
    {
        var r = new RecorrenciaCompra(Guid.NewGuid(), Guid.NewGuid(), DataBase, 45m, 2);

        Assert.Equal(45m, r.IntervaloMedioDias);
        Assert.Equal(2, r.QuantidadeCompras);
        Assert.Equal(DataBase, r.DataUltimaCompra);
    }

    [Fact]
    public void Atualizar_DeveSubstituirValores()
    {
        var r = new RecorrenciaCompra(Guid.NewGuid(), Guid.NewGuid(), DataBase, 45m, 2);
        var novaData = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        r.Atualizar(novaData, 37.5m, 3);

        Assert.Equal(novaData, r.DataUltimaCompra);
        Assert.Equal(37.5m, r.IntervaloMedioDias);
        Assert.Equal(3, r.QuantidadeCompras);
    }
}