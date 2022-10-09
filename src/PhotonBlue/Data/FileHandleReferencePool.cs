using System.Collections.Concurrent;

namespace PhotonBlue.Data;

public class FileHandleReferencePool : IObjectPool<WeakReference<BaseFileHandle>, BaseFileHandle>, IDisposable
{
    private const int MaxConcurrency = 10000;
    private const int MinConcurrency = 0;

    private readonly ConcurrentQueue<WeakReference<BaseFileHandle>> _items;
    private readonly SemaphoreSlim _semaphore;

    public FileHandleReferencePool()
    {
        _items = new ConcurrentQueue<WeakReference<BaseFileHandle>>();
        _semaphore = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
    }

    public WeakReference<BaseFileHandle> Acquire(BaseFileHandle data)
    {
        _semaphore.Wait();

        if (_items.TryDequeue(out var handle))
        {
            handle.SetTarget(data);
            return handle;
        }

        handle = new WeakReference<BaseFileHandle>(data);
        return handle;
    }

    public void Release(WeakReference<BaseFileHandle> handle)
    {
        // If we have too many surplus weak references, just let them fall out of scope
        if (MaxConcurrency - _semaphore.CurrentCount <= MinConcurrency)
        {
            _items.Enqueue(handle);
        }

        _semaphore.Release();
    }

    public void Dispose()
    {
        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}