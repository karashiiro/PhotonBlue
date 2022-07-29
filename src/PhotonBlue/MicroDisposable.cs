namespace PhotonBlue;

/// <summary>
/// A value-typed disposable object that can be used for deferred cleanup actions.
/// This is intended for when creating a specific wrapper type would be overkill.
/// </summary>
/// <typeparam name="T">The type of the object to wrap.</typeparam>
public struct MicroDisposable<T> : IDisposable
{
    private readonly T disposeArg;
    private readonly Action<T> disposeAction;

    private bool disposed;

    private MicroDisposable(T disposeTarget, Action<T> dispose)
    {
        disposeArg = disposeTarget;
        disposeAction = dispose;
        disposed = false;
    }
        
    public void Dispose()
    {
        if (!disposed)
        {
            disposed = true;
            disposeAction(disposeArg);
        }
    }

    public static MicroDisposable<T> Create(T disposeTarget, Action<T> dispose)
    {
        return new MicroDisposable<T>(disposeTarget, dispose);
    }
}