using PhotonBlue.Data;

namespace PhotonBlue;

public class GameData
{
    /// <summary>
    /// The current data path that Photon Blue is using to load files.
    /// </summary>
    public DirectoryInfo DataPath { get; }

    /// <summary>
    /// Provides access to the <see cref="FileHandleManager"/> which allows you to create new <see cref="FileHandle{T}"/>s which then allows you to
    /// easily defer file loading onto another thread.
    /// </summary>
    public FileHandleManager FileHandleManager { get; }

    public GameData(string pso2BinPath)
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

        FileHandleManager = new FileHandleManager();
    }

    /// <summary>
    /// Fetches a file given a data path relative to pso2_bin.
    /// </summary>
    /// <param name="path"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T? GetFile<T>(string path) where T : FileResource, new()
    {
        var file = new FileHandle<T>(Path.Combine(DataPath.FullName, path));
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
        var file = new FileHandle<T>(Path.Combine(DataPath.FullName, path));
        file.LoadHeadersOnly();
        return file.State != BaseFileHandle.FileState.Loaded ? null : file.Value;
    }

    /// <summary>
    /// Fetches a file given a filesystem path.
    /// </summary>
    /// <param name="path"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T? GetFileFromDisk<T>(string path) where T : FileResource, new()
    {
        var file = new FileHandle<T>(path);
        file.Load();
        return file.State != BaseFileHandle.FileState.Loaded ? null : file.Value;
    }

    /// <summary>
    /// Fetches a file given a filesystem path, parsing only its headers.
    /// </summary>
    /// <param name="path"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T? GetFileFromDiskHeadersOnly<T>(string path) where T : FileResource, new()
    {
        var file = new FileHandle<T>(path);
        file.LoadHeadersOnly();
        return file.State != BaseFileHandle.FileState.Loaded ? null : file.Value;
    }

    /// <summary>
    /// Creates a new handle to a game file but does not load it. You will need to call <see cref="ProcessFileHandleQueue"/> yourself for these handles
    /// to be loaded, on a different thread.
    /// </summary>
    /// <param name="path">The path to the file to load</param>
    /// <typeparam name="T">The type of <see cref="FileResource"/> to load</typeparam>
    /// <returns>A handle to the file to be loaded</returns>
    public FileHandle<T> GetFileHandle<T>(string path) where T : FileResource, new()
    {
        return FileHandleManager.CreateHandle<T>(path);
    }

    /// <summary>
    /// Processes enqueued file handles that haven't been loaded yet. Call this on a different thread to process handles.
    /// </summary>
    public void ProcessFileHandleQueue()
    {
        FileHandleManager.ProcessQueue();
    }
}