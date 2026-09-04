namespace AndreGas.Web.Common;

/// <summary>
/// Notifica componentes do mesmo circuito Blazor Server (serviço scoped) de que os dados de
/// vendas mudaram, permitindo que telas como o Dashboard se atualizem sem recarregar a página.
/// </summary>
public sealed class VendasAtualizadasNotifier
{
    private event Func<Task>? _vendasAtualizadas;

    public void NotificarVendaRegistrada() => _vendasAtualizadas?.Invoke();

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
