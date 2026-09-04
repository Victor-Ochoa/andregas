namespace AndreGas.Web.Common;

/// <summary>
/// Notifica componentes do mesmo circuito Blazor Server (serviço scoped) de que os dados de
/// vendas mudaram, permitindo que telas como o Dashboard se atualizem sem recarregar a página.
/// </summary>
public sealed class VendasAtualizadasNotifier
{
    private event Func<Task>? _vendasAtualizadas;

    /// <summary>
    /// Notifica e aguarda todos os inscritos (sequencialmente). Ao ser aguardado, garante que
    /// consultas disparadas por outros componentes do mesmo circuito (ex.: Dashboard) concluam
    /// antes do chamador continuar usando o mesmo DbContext scoped — evita uso concorrente do
    /// DbContext (InvalidOperationException).
    /// </summary>
    public async Task NotificarVendaRegistrada()
    {
        if (_vendasAtualizadas is null)
        {
            return;
        }

        foreach (var handler in _vendasAtualizadas.GetInvocationList().Cast<Func<Task>>())
        {
            await handler();
        }
    }

    public IDisposable Subscribe(Func<Task> handler)
    {
        _vendasAtualizadas += handler;
        return new Unsubscriber(this, handler);
    }

    private sealed class Unsubscriber(VendasAtualizadasNotifier owner, Func<Task> handler) : IDisposable
    {
        public void Dispose() => owner._vendasAtualizadas -= handler;
    }
}
