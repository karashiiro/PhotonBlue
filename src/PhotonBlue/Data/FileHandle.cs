namespace PhotonBlue.Data;

public class FileHandle<T> : BaseFileHandle where T : FileResource, new()
{
    internal FileHandle(string path) : base(path)
    {
    }

    public override void Load()
    {
        State = FileState.Loading;
        LoadException = null;

        try
        {
            using var data = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
                FileOptions.SequentialScan);
            var file = FileResource.FromStream<T>(data);
            file.LoadFile();

            State = FileState.Loaded;
            Instance = file;
        }
        catch (Exception e)
        {
            LoadException = e;
            State = FileState.Error;
        }
    }

    public override void LoadHeadersOnly()
    {
        State = FileState.Loading;

        try
        {
            using var data = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
                FileOptions.SequentialScan);
            var file = FileResource.FromStream<T>(data);
            file.LoadHeadersOnly();

            State = FileState.Loaded;
            Instance = file;
        }
        catch
        {
            State = FileState.Error;
        }
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