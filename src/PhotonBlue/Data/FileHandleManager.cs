using PhotonBlue.Cryptography;

namespace PhotonBlue.Data;

public sealed class FileHandleManager : IFileHandleProvider
{
    private readonly IObjectPool<BlowfishGpuHandle, Blowfish> _blowfishGpuPool;

    public FileHandleManager(IObjectPool<BlowfishGpuHandle, Blowfish> blowfishGpuPool)
    {
        _blowfishGpuPool = blowfishGpuPool;
    }

    /// <inheritdoc />
    public FileHandle<T> CreateHandle<T>(string path, bool loadComplete = true) where T : FileResource, new()
    {
        var handle = new FileHandle<T>(path, _blowfishGpuPool);
        var weakRef = new WeakReference<BaseFileHandle>(handle);
        ThreadPool.QueueUserWorkItem(loadComplete ? LoadFileHandleCallback : LoadFileHandleHeadersOnlyCallback,
            weakRef);
        return handle;
    }

    // These delegate allocations are cached to avoid excessive memory usage (profiled).
    private static readonly WaitCallback LoadFileHandleCallback = LoadFileHandle;
    private static readonly WaitCallback LoadFileHandleHeadersOnlyCallback = LoadFileHandleHeadersOnly;

    private static void LoadFileHandle(object? o)
    {
        if (o is WeakReference<BaseFileHandle> weakRef)
        {
            LoadFileHandle(weakRef);
        }
    }

    private static void LoadFileHandleHeadersOnly(object? o)
    {
        if (o is WeakReference<BaseFileHandle> weakRef)
        {
            LoadFileHandleHeadersOnly(weakRef);
        }
    }

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
}