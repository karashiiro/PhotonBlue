using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using ComputeSharp;

namespace PhotonBlue.Cryptography;

public sealed class BlowfishGpuBufferPool : IObjectPool<BlowfishGpuHandle, Blowfish>, IDisposable
{
    public const int DataBufferSize = 524288;

    private const int MaxConcurrency = 50;
    private const int MinConcurrency = 5;

    private static readonly GraphicsDevice Gpu = GraphicsDevice.GetDefault();

    private readonly ConcurrentQueue<BlowfishGpuHandle> _items;
    private readonly SemaphoreSlim _semaphore;

    public BlowfishGpuBufferPool()
    {
        _items = new ConcurrentQueue<BlowfishGpuHandle>();
        _semaphore = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
    }

    public BlowfishGpuHandle Acquire(Blowfish state)
    {
        _semaphore.Wait();

        if (_items.TryDequeue(out var handle))
        {
            // This is a lifetime escape, but we're just going to tolerate it for now.
            // So long as this function is aware that these arrays came from an ArrayPool,
            // it's probably fine.
            handle.S0.CopyFrom(state.S[0].AsSpan(0, 256));
            handle.S1.CopyFrom(state.S[1].AsSpan(0, 256));
            handle.S2.CopyFrom(state.S[2].AsSpan(0, 256));
            handle.S3.CopyFrom(state.S[3].AsSpan(0, 256));
            handle.P.CopyFrom(state.P.AsSpan(0, 18));
            return handle;
        }

        var elementSize = Unsafe.SizeOf<uint2>();
        handle = new BlowfishGpuHandle
        {
            Upload = Gpu.AllocateUploadBuffer<uint2>(DataBufferSize / elementSize),
            Download = Gpu.AllocateReadBackBuffer<uint2>(DataBufferSize / elementSize),
            S0 = Gpu.AllocateConstantBuffer<uint>(state.S[0].AsSpan(0, 256)),
            S1 = Gpu.AllocateConstantBuffer<uint>(state.S[1].AsSpan(0, 256)),
            S2 = Gpu.AllocateConstantBuffer<uint>(state.S[2].AsSpan(0, 256)),
            S3 = Gpu.AllocateConstantBuffer<uint>(state.S[3].AsSpan(0, 256)),
            P = Gpu.AllocateConstantBuffer<uint>(state.P.AsSpan(0, 18)),
            Data = Gpu.AllocateReadWriteBuffer<uint2>(DataBufferSize / elementSize),
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