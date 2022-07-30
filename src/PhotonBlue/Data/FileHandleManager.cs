using Amib.Threading;

namespace PhotonBlue.Data;

public sealed class FileHandleManager : IFileHandleProvider, IDisposable
{
    private readonly SmartThreadPool _threadPool;

    public FileHandleManager()
    {
        _threadPool = new SmartThreadPool(new STPStartInfo
        {
            ThreadPoolName = "Photon Blue",
        });
    }

    /// <inheritdoc />
    public FileHandle<T> CreateHandle<T>(string path, bool loadComplete = true) where T : FileResource, new()
    {
        var handle = new FileHandle<T>(path);
        var weakRef = new WeakReference<BaseFileHandle>(handle);
        _threadPool.QueueWorkItem(loadComplete ? LoadFileHandleAction : LoadFileHandleHeadersOnlyAction, weakRef);
        return handle;
    }

    // These two actions are pre-allocated to avoid frequent boxing. Boxing otherwise accounts for
    // over a gigabyte of short-lived allocations over the course of an indexing job.

    // ReSharper disable once ConvertClosureToMethodGroup
    private static readonly Action<WeakReference<BaseFileHandle>> LoadFileHandleAction =
        weakRef => LoadFileHandle(weakRef);

    // ReSharper disable once ConvertClosureToMethodGroup
    private static readonly Action<WeakReference<BaseFileHandle>> LoadFileHandleHeadersOnlyAction =
        weakRef => LoadFileHandleHeadersOnly(weakRef);

    private static void LoadFileHandle(WeakReference<BaseFileHandle> weakRef)
    {
        if (weakRef.TryGetTarget(out var handle))
        {
            handle.Load();
        }
    }

    private static void LoadFileHandleHeadersOnly(WeakReference<BaseFileHandle> weakRef)
    {
        if (weakRef.TryGetTarget(out var handle))
        {
            handle.LoadHeadersOnly();
        }
    }

    public void Dispose()
    {
        _threadPool.Dispose();
    }
}