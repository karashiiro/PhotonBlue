using System.Collections.Concurrent;
using ComputeSharp;

namespace PhotonBlue.Cryptography;

public sealed class BlowfishGpuBufferPool : IDisposable
{
    private const int Concurrency = 8;

    private readonly ConcurrentQueue<BlowfishGpuHandle> _items;
    private readonly SemaphoreSlim _semaphore;

    public BlowfishGpuBufferPool()
    {
        _items = new ConcurrentQueue<BlowfishGpuHandle>();
        _semaphore = new SemaphoreSlim(Concurrency, Concurrency);
    }

    public BlowfishGpuHandle Acquire(Blowfish state)
    {
        _semaphore.Wait();

        if (_items.TryDequeue(out var handle))
        {
            handle.S0.CopyFrom(state.S[0]);
            handle.S1.CopyFrom(state.S[1]);
            handle.S2.CopyFrom(state.S[2]);
            handle.S3.CopyFrom(state.S[3]);
            handle.P.CopyFrom(state.P);
            return handle;
        }

        handle = new BlowfishGpuHandle
        {
            S0 = GraphicsDevice.Default.AllocateReadOnlyBuffer(state.S[0]),
            S1 = GraphicsDevice.Default.AllocateReadOnlyBuffer(state.S[1]),
            S2 = GraphicsDevice.Default.AllocateReadOnlyBuffer(state.S[2]),
            S3 = GraphicsDevice.Default.AllocateReadOnlyBuffer(state.S[3]),
            P = GraphicsDevice.Default.AllocateConstantBuffer(state.P),
        };

        return handle;
    }

    public void Release(BlowfishGpuHandle handle)
    {
        if (_semaphore.CurrentCount > Concurrency / 4)
        {
            handle.Dispose();
        }
        else
        {
            _items.Enqueue(handle);
        }

        _semaphore.Release();
    }

    public void Dispose()
    {
        _semaphore.Dispose();
        foreach (var item in _items)
        {
            item.Dispose();
        }
    }
}