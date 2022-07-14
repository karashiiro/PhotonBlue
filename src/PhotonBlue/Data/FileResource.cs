namespace PhotonBlue.Data;

public abstract class FileResource
{
    protected BinaryReader Reader { get; }

    protected FileResource(Stream data)
    {
        Reader = new BinaryReader(data);
    }

    protected abstract void LoadFile();
}