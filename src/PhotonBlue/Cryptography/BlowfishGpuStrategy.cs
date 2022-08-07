using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ComputeSharp;

namespace PhotonBlue.Cryptography;

internal class BlowfishGpuStrategy : BlowfishStrategy
{
    public const int RecommendedThreshold = 8192;

    private readonly Blowfish _blowfish;
    private readonly BlowfishGpuHandle _buffers;
    private readonly IObjectPool<BlowfishGpuHandle, Blowfish> _gpuPool;

    private bool _disposed;

    public BlowfishGpuStrategy(IObjectPool<BlowfishGpuHandle, Blowfish> gpuPool, Blowfish blowfish)
    {
        _blowfish = blowfish;
        _gpuPool = gpuPool;
        _buffers = _gpuPool.Acquire(_blowfish);
    }

    public override void Decrypt(Span<byte> data)
    {
        Debug.Assert(data.Length % 8 == 0, "Decrypt payload is not a multiple of 8 bytes long.");

        var dataEx = MemoryMarshal.Cast<byte, uint2>(data);
        var elementSize = Unsafe.SizeOf<uint2>();
        for (var i = 0; i < dataEx.Length; i += _buffers.Data.Length)
        {
            // Calculate the length of the data to decrypt
            var len = Math.Min(_buffers.Data.Length, dataEx[i..].Length);
            if (len < RecommendedThreshold)
            {
                // Decrypt small blocks of data on the CPU, even though we have buffers set up already.
                _blowfish.DecryptStandard(data.Slice(i * elementSize, len * elementSize));
            }
            else
            {
                // Select the data we want to operate on
                var dataSlice = dataEx.Slice(i, len);

                // Copy that data onto the GPU
                _buffers.Data.CopyFrom(dataSlice);

                // Run the compute shader
                GraphicsDevice.GetDefault().For(len, 1, 1, 8, 8, 1,
                    new BlowfishShader(_buffers.S0, _buffers.S1, _buffers.S2, _buffers.S3, _buffers.P, _buffers.Data));

                // Copy data back from the GPU
                _buffers.Data.CopyTo(dataSlice);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _gpuPool.Release(_buffers);
            _disposed = true;
        }

        base.Dispose(disposing);
    }
}