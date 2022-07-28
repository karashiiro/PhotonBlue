namespace PhotonBlue;

public sealed class DisposableBundle : IDisposable
{
    // This should refer to an immutable subclass, but for now it doesn't.
    public static readonly DisposableBundle Empty = new();
    
    public List<IDisposable> Objects { get; } = new();

    public void Dispose()
    {
        foreach (var o in Objects)
        {
            o.Dispose();
        }
    }
}