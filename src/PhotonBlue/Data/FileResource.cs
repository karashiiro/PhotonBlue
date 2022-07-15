namespace PhotonBlue.Data;

public abstract class FileResource : IDisposable
{
    protected BinaryReader Reader { get; }

    protected FileResource(Stream data)
    {
        Reader = new BinaryReader(data);
    }

    public abstract void LoadFile();

    protected void Dispose(bool disposing)
    {
        if (disposing)
        {
            Reader.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}