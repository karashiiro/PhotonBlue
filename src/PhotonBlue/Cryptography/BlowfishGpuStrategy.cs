using System.Diagnostics;
using System.Runtime.CompilerServices;
using ComputeSharp;

namespace PhotonBlue.Cryptography;

public class BlowfishGpuStrategy : BlowfishStrategy
{
    public const int RecommendedThreshold = 65536;

    private readonly ReadWriteBuffer<uint2> _gpuBuffer;
    private readonly Blowfish _blowfish;
    private readonly BlowfishGpuHandle _boxes;

    private bool _disposed;

    // TODO: Set up dependency injection or something
    private static readonly BlowfishGpuBufferPool GpuPool = new();

    public unsafe BlowfishGpuStrategy(IEnumerable<byte> key, int bufferSize)
    {
        _blowfish = new Blowfish(key);
        _boxes = GpuPool.Acquire(_blowfish);

        var scaledBufferSize = bufferSize / sizeof(uint2);
        _gpuBuffer = GraphicsDevice.Default.AllocateReadWriteBuffer<uint2>(scaledBufferSize);
    }

    public override unsafe void Decrypt(Span<byte> data)
    {
        Debug.Assert(data.Length % 8 == 0, "Decrypt payload is not a multiple of 8 bytes long.");

        var dataEx = new Span<uint2>(Unsafe.AsPointer(ref data[0]), data.Length / sizeof(uint2));
        for (var i = 0; i < dataEx.Length; i += _gpuBuffer.Length)
        {
            // Calculate the length of the data to decrypt
            var len = Math.Min(_gpuBuffer.Length, dataEx[i..].Length);
            if (len < RecommendedThreshold)
            {
                // Decrypt small blocks of data on the CPU, even though we have buffers set up already.
                _blowfish.DecryptStandard(data.Slice(i * sizeof(uint2), len * sizeof(uint2)));
            }
            else
            {
                var dataSlice = dataEx.Slice(i, len);

                // Copy that data into the work buffer
                _gpuBuffer.CopyFrom(dataSlice);

                // Run the compute shader
                GraphicsDevice.Default.For(len, 1, 1, 8, 8, 1,
                    new BlowfishShader(_boxes.S0, _boxes.S1, _boxes.S2, _boxes.S3, _boxes.P, _gpuBuffer));

                // Copy data from the work buffer into main memory
                _gpuBuffer.CopyTo(dataSlice);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _gpuBuffer.Dispose();
            GpuPool.Release(_boxes);

            _disposed = true;
        }

        base.Dispose(disposing);
    }
}