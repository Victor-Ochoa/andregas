using AndreGas.Web.Common;

namespace AndreGas.UnitTests.Common;

public class VendasAtualizadasNotifierTests
{
    [Fact]
    public async Task NotificarVendaRegistrada_DeveAguardarOsInscritos()
    {
        var notifier = new VendasAtualizadasNotifier();
        var aguardou = false;
        var tcs = new TaskCompletionSource();
        using var sub = notifier.Subscribe(async () =>
        {
            await tcs.Task;
            aguardou = true;
        });

        var notificar = notifier.NotificarVendaRegistrada();
        Assert.False(aguardou, "O inscrito não deve ter concluído antes do notifier aguardar.");

        tcs.SetResult();
        await notificar;

        Assert.True(aguardou, "O notifier deve aguardar a conclusão dos inscritos antes de retornar.");
    }

    [Fact]
    public async Task NotificarVendaRegistrada_DeveChamarTodosOsInscritosEmOrdem()
    {
        var notifier = new VendasAtualizadasNotifier();
        var ordem = new List<int>();
        using var sub1 = notifier.Subscribe(() => { ordem.Add(1); return Task.CompletedTask; });
        using var sub2 = notifier.Subscribe(() => { ordem.Add(2); return Task.CompletedTask; });

        await notifier.NotificarVendaRegistrada();

        Assert.Equal([1, 2], ordem);
    }

    [Fact]
    public async Task NotificarVendaRegistrada_SemInscritos_NaoLancaErro()
    {
        var notifier = new VendasAtualizadasNotifier();
        await notifier.NotificarVendaRegistrada();
    }
}