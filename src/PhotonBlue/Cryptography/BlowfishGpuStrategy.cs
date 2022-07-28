using System.Diagnostics;
using System.Runtime.CompilerServices;
using ComputeSharp;

namespace PhotonBlue.Cryptography;

public class BlowfishGpuStrategy : BlowfishStrategy
{
    public const int RecommendedThreshold = 32768;

    private readonly Blowfish _blowfish;
    private readonly BlowfishGpuHandle _buffers;

    private bool _disposed;

    // TODO: Set up dependency injection or something
    private static readonly BlowfishGpuBufferPool GpuPool = new();

    public BlowfishGpuStrategy(ReadOnlySpan<byte> key)
    {
        _blowfish = new Blowfish(key);
        _buffers = GpuPool.Acquire(_blowfish);
    }

    public override unsafe void Decrypt(Span<byte> data)
    {
        Debug.Assert(data.Length % 8 == 0, "Decrypt payload is not a multiple of 8 bytes long.");

        var dataEx = new Span<uint2>(Unsafe.AsPointer(ref data[0]), data.Length / sizeof(uint2));
        for (var i = 0; i < dataEx.Length; i += _buffers.Data.Length)
        {
            // Calculate the length of the data to decrypt
            var len = Math.Min(_buffers.Data.Length, dataEx[i..].Length);
            if (len < RecommendedThreshold)
            {
                // Decrypt small blocks of data on the CPU, even though we have buffers set up already.
                _blowfish.DecryptStandard(data.Slice(i * sizeof(uint2), len * sizeof(uint2)));
            }
            else
            {
                var dataSlice = dataEx.Slice(i, len);

                // Copy that data into the work buffer
                _buffers.Data.CopyFrom(dataSlice);

                // Run the compute shader
                GraphicsDevice.Default.For(len, 1, 1, 8, 8, 1,
                    new BlowfishShader(_buffers.S0, _buffers.S1, _buffers.S2, _buffers.S3, _buffers.P, _buffers.Data));

                // Copy data from the work buffer into main memory
                _buffers.Data.CopyTo(dataSlice);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            GpuPool.Release(_buffers);
            _disposed = true;
        }

        base.Dispose(disposing);
    }
}