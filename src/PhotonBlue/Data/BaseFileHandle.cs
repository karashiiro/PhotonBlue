using PhotonBlue.Cryptography;

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

    internal BaseFileHandle(string path, IObjectPool<BlowfishGpuHandle, Blowfish> blowfishGpuPool)
    {
        Reset = new ManualResetEventSlim();
        State = FileState.None;
        Path = path;
        BlowfishGpuPool = blowfishGpuPool;
    }

    public ManualResetEventSlim Reset { get; }
    public FileState State { get; protected set; }
    public Exception? LoadException { get; protected set; }
    protected readonly string Path;
    protected readonly IObjectPool<BlowfishGpuHandle, Blowfish> BlowfishGpuPool;
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