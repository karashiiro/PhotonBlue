using PhotonBlue.Cryptography;

namespace PhotonBlue.Data;

public sealed class FileHandleManager : IFileHandleProvider
{
    private readonly IObjectPool<BlowfishGpuHandle, Blowfish> _blowfishGpuPool;
    private readonly IObjectPool<WeakReference<BaseFileHandle>, BaseFileHandle> _weakRefPool;

    // These delegate allocations are cached to avoid excessive memory usage (profiled).
    private readonly WaitCallback _loadFileHandleCallback;
    private readonly WaitCallback _loadFileHandleHeadersOnlyCallback;

    public FileHandleManager(IObjectPool<BlowfishGpuHandle, Blowfish> blowfishGpuPool,
        IObjectPool<WeakReference<BaseFileHandle>, BaseFileHandle> weakRefPool)
    {
        _blowfishGpuPool = blowfishGpuPool;
        _weakRefPool = weakRefPool;
        _loadFileHandleCallback = LoadFileHandle;
        _loadFileHandleHeadersOnlyCallback = LoadFileHandleHeadersOnly;
    }

    /// <inheritdoc />
    public FileHandle<T> CreateHandle<T>(string path, bool loadComplete = true) where T : FileResource, new()
    {
        var handle = new FileHandle<T>(path, _blowfishGpuPool);
        var weakRef = _weakRefPool.Acquire(handle);
        ThreadPool.QueueUserWorkItem(loadComplete ? _loadFileHandleCallback : _loadFileHandleHeadersOnlyCallback,
            weakRef);
        return handle;
    }

    private void LoadFileHandle(object? o)
    {
        if (o is WeakReference<BaseFileHandle> weakRef)
        {
            LoadFileHandle(weakRef);
        }
    }

    private void LoadFileHandleHeadersOnly(object? o)
    {
        if (o is WeakReference<BaseFileHandle> weakRef)
        {
            LoadFileHandleHeadersOnly(weakRef);
        }
    }

    private void LoadFileHandle(WeakReference<BaseFileHandle> weakRef)
    {
        if (weakRef.TryGetTarget(out var handle))
        {
            handle.Load();
        }

        _weakRefPool.Release(weakRef);
    }

    private void LoadFileHandleHeadersOnly(WeakReference<BaseFileHandle> weakRef)
    {
        if (weakRef.TryGetTarget(out var handle))
        {
            handle.LoadHeadersOnly();
        }

        _weakRefPool.Release(weakRef);
    }
}