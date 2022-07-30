using Ninject;
using PhotonBlue.Cryptography;
using PhotonBlue.Data;
using PhotonBlue.Persistence;

namespace PhotonBlue;

public sealed class GameData : IDisposable
{
    /// <summary>
    /// The current data path that Photon Blue is using to load files.
    /// </summary>
    public DirectoryInfo DataPath { get; }

    public IGameFileIndexer Indexer => _kernel.Get<IGameFileIndexer>();

    public IGameFileIndex Index => _kernel.Get<IGameFileIndex>();

    /// <summary>
    /// Provides access to the <see cref="IFileHandleProvider"/> which allows you to create new
    /// <see cref="FileHandle{T}"/>s which then allows you to easily defer file loading onto another thread.
    /// </summary>
    public IFileHandleProvider FileHandleProvider => _kernel.Get<IFileHandleProvider>();

    private readonly IKernel _kernel;

    public GameData(string pso2BinPath, Func<IGameFileIndex>? indexProvider = null)
    {
        DataPath = new DirectoryInfo(pso2BinPath);

        if (!DataPath.Exists)
        {
            throw new DirectoryNotFoundException("DataPath provided is missing.");
        }

        if (DataPath.Name != "pso2_bin")
        {
            throw new ArgumentException("DataPath must point to the pso2_bin directory.", nameof(pso2BinPath));
        }

        _kernel = new StandardKernel(new ServiceModule(indexProvider));
    }

    /// <summary>
    /// Fetches a file given a data path relative to pso2_bin.
    /// </summary>
    /// <param name="path"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T? GetFile<T>(string path) where T : FileResource, new()
    {
        var blowfishGpuPool = _kernel.Get<IObjectPool<BlowfishGpuHandle, Blowfish>>();
        var file = new FileHandle<T>(Path.Combine(DataPath.FullName, path), blowfishGpuPool);
        file.Load();
        return file.State != BaseFileHandle.FileState.Loaded ? null : file.Value;
    }

    /// <summary>
    /// Fetches a file given a data path relative to pso2_bin, parsing only its headers.
    /// </summary>
    /// <param name="path"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T? GetFileHeadersOnly<T>(string path) where T : FileResource, new()
    {
        var blowfishGpuPool = _kernel.Get<IObjectPool<BlowfishGpuHandle, Blowfish>>();
        var file = new FileHandle<T>(Path.Combine(DataPath.FullName, path), blowfishGpuPool);
        file.LoadHeadersOnly();
        return file.State != BaseFileHandle.FileState.Loaded ? null : file.Value;
    }

    /// <summary>
    /// Fetches a file given a filesystem path.
    /// </summary>
    /// <param name="path"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T? GetFileFromDisk<T>(string path) where T : FileResource, new()
    {
        var blowfishGpuPool = _kernel.Get<IObjectPool<BlowfishGpuHandle, Blowfish>>();
        var file = new FileHandle<T>(path, blowfishGpuPool);
        file.Load();
        return file.State != BaseFileHandle.FileState.Loaded ? null : file.Value;
    }

    /// <summary>
    /// Fetches a file given a filesystem path, parsing only its headers.
    /// </summary>
    /// <param name="path"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T? GetFileFromDiskHeadersOnly<T>(string path) where T : FileResource, new()
    {
        var blowfishGpuPool = _kernel.Get<IObjectPool<BlowfishGpuHandle, Blowfish>>();
        var file = new FileHandle<T>(path, blowfishGpuPool);
        file.LoadHeadersOnly();
        return file.State != BaseFileHandle.FileState.Loaded ? null : file.Value;
    }

    /// <summary>
    /// Creates a file handle using the file handle manager. The manager's queue must be processed
    /// for the file to be loaded.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="loadComplete"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public FileHandle<T> GetFileHandle<T>(string path, bool loadComplete = true) where T : FileResource, new()
    {
        return FileHandleProvider.CreateHandle<T>(path, loadComplete);
    }

    public void Dispose()
    {
        _kernel.Dispose();
    }
}