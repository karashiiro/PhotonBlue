namespace PhotonBlue;

/// <summary>
/// A value-typed disposable object that can be used for deferred cleanup actions.
/// This is intended for when creating a specific wrapper type would be overkill.
/// </summary>
/// <typeparam name="T">The type of the object to wrap.</typeparam>
public readonly struct MicroDisposable<T> : IDisposable
{
    private readonly T disposeArg;
    private readonly Action<T> disposeAction;

    private MicroDisposable(T disposeTarget, Action<T> dispose)
    {
        disposeArg = disposeTarget;
        disposeAction = dispose;
    }
        
    public void Dispose()
    {
        disposeAction(disposeArg);
    }

    public static MicroDisposable<T> Create(T disposeTarget, Action<T> dispose)
    {
        return new MicroDisposable<T>(disposeTarget, dispose);
    }
}