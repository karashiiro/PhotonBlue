namespace PhotonBlue;

public sealed class DisposableBundle : IDisposable
{
    public List<IDisposable> Objects { get; } = new();

    public void Dispose()
    {
        foreach (var o in Objects)
        {
            o.Dispose();
        }
    }
}