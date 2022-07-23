using System.IO.MemoryMappedFiles;

namespace PhotonBlue.Data;

public class FileHandle<T> : BaseFileHandle where T : FileResource, new()
{
    internal FileHandle(string path) : base(path)
    {
    }

    public override void Load()
    {
        BeginLoad();
        try
        {
            using var mmf =
                MemoryMappedFile.CreateFromFile(Path, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            using var data = mmf.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
            using var buffer = new BufferedStream(data);
            var file = FileResource.FromStream<T>(buffer);
            file.LoadFile();

            State = FileState.Loaded;
            Instance = file;
        }
        catch (Exception e)
        {
            HandleException(e);
        }
    }

    public override void LoadHeadersOnly()
    {
        BeginLoad();
        try
        {
            using var mmf =
                MemoryMappedFile.CreateFromFile(Path, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            using var data = mmf.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
            using var buffer = new BufferedStream(data);
            var file = FileResource.FromStream<T>(buffer);
            file.LoadHeadersOnly();

            State = FileState.Loaded;
            Instance = file;
        }
        catch (Exception e)
        {
            HandleException(e);
        }
    }

    private void BeginLoad()
    {
        State = FileState.Loading;
        LoadException = null;
    }

    private void HandleException(Exception e)
    {
        LoadException = e;
        State = FileState.Error;
    }

    /// <summary>
    /// Returns the <see cref="FileResource"/> or null if it isn't loaded or failed to load.
    /// </summary>
    public T? Value
    {
        get
        {
            if (HasValue)
            {
                return (T?)Instance;
            }

            return null;
        }
    }
}