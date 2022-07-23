namespace PhotonBlue.Data;

public abstract class BaseFileHandle
{
    public enum FileState
    {
        None,
        Loading,
        Loaded,
        Error,
    }

    internal BaseFileHandle(string path)
    {
        Path = path;
    }

    public FileState State { get; protected set; } = FileState.None;
    protected readonly string Path;
    protected object? Instance;

    public bool HasValue => State == FileState.Loaded && Instance != null;

    /// <summary>
    /// Loads the underlying <see cref="FileResource"/>
    /// </summary>
    public abstract void Load();
    
    /// <summary>
    /// Loads the underlying <see cref="FileResource"/>'s headers, without loading
    /// any nonessential data.
    /// </summary>
    /// <exception cref="NotSupportedException"></exception>
    public virtual void LoadHeadersOnly()
    {
        throw new NotSupportedException();
    }
}