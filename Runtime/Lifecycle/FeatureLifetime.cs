namespace OmniToolbox.Lifecycle;

public sealed class FeatureLifetime : IDisposable
{
    private readonly List<Action> disposers = [];
    private bool disposed;

    public void Add(Action disposer)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        disposers.Add(disposer);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        List<Exception>? errors = null;
        for (var i = disposers.Count - 1; i >= 0; i--)
        {
            try
            {
                disposers[i]();
            }
            catch (Exception ex)
            {
                (errors ??= []).Add(ex);
            }
        }

        disposers.Clear();
        if (errors is not null)
        {
            throw new AggregateException("One or more feature disposers failed.", errors);
        }
    }
}
