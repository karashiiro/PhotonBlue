namespace PhotonBlue.Data;

public abstract class FileResource
{
    internal Stream? BaseStream { get; set; }
    internal BinaryReader? Reader { get; set; }

    protected FileResource()
    {
    }

    protected FileResource(Stream data)
    {
        BaseStream = data;
        Reader = new BinaryReader(data);
    }

    public abstract void LoadFile();

    /// <summary>
    /// Loads the file's headers, without loading any nonessential data.
    /// This may not be meaningfully implemented for some files.
    /// </summary>
    /// <exception cref="NotSupportedException"></exception>
    public virtual void LoadHeadersOnly()
    {
        throw new NotSupportedException();
    }
}