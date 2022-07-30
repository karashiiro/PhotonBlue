namespace PhotonBlue.Cryptography;

internal abstract class BlowfishStrategy : IDisposable
{
    public abstract void Decrypt(Span<byte> data);

    protected virtual void Dispose(bool disposing)
    {
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}