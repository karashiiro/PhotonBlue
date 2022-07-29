using System.Collections.Concurrent;
using ComputeSharp;

namespace PhotonBlue.Cryptography;

public sealed class BlowfishGpuBufferPool : IDisposable
{
    public const int DataBufferSize = 524288;
    
    private const int MaxConcurrency = 32;
    private const int MinConcurrency = 1;
    
    private static readonly GraphicsDevice Gpu = GraphicsDevice.Default;

    private readonly ConcurrentQueue<BlowfishGpuHandle> _items;
    private readonly SemaphoreSlim _semaphore;

    public BlowfishGpuBufferPool()
    {
        _items = new ConcurrentQueue<BlowfishGpuHandle>();
        _semaphore = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
    }

    public unsafe BlowfishGpuHandle Acquire(Blowfish state)
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
            S0 = Gpu.AllocateConstantBuffer(state.S[0]),
            S1 = Gpu.AllocateConstantBuffer(state.S[1]),
            S2 = Gpu.AllocateConstantBuffer(state.S[2]),
            S3 = Gpu.AllocateConstantBuffer(state.S[3]),
            P = Gpu.AllocateConstantBuffer(state.P),
            Data = Gpu.AllocateReadWriteBuffer<uint2>(DataBufferSize / sizeof(uint2)),
        };

        return handle;
    }

    public void Release(BlowfishGpuHandle handle)
    {
        if (MaxConcurrency - _semaphore.CurrentCount > MinConcurrency)
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