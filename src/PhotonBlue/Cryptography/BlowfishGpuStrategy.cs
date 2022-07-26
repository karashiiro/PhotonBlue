using System.Diagnostics;
using System.Runtime.CompilerServices;
using ComputeSharp;

namespace PhotonBlue.Cryptography;

public class BlowfishGpuStrategy : BlowfishStrategy
{
    public const int RecommendedThreshold = 65536;
    private const int InternalCpuThreshold = 1024;

    private readonly UploadBuffer<uint2> _gpuUpload;
    private readonly ReadWriteBuffer<uint2> _gpuBuffer;
    private readonly ReadBackBuffer<uint2> _gpuDownload;
    private readonly Blowfish _blowfish;
    private readonly Blowfish.GpuHandle _boxes;

    public BlowfishGpuStrategy(IEnumerable<byte> key, int bufferSize)
    {
        _blowfish = new Blowfish(key);
        _boxes = _blowfish.AllocateToGraphicsDevice(GraphicsDevice.Default);
        _gpuBuffer = GraphicsDevice.Default.AllocateReadWriteBuffer<uint2>(bufferSize / 8);
        _gpuUpload = GraphicsDevice.Default.AllocateUploadBuffer<uint2>(bufferSize / 8);
        _gpuDownload = GraphicsDevice.Default.AllocateReadBackBuffer<uint2>(bufferSize / 8);
    }

    public override unsafe void Decrypt(Span<byte> data)
    {
        Debug.Assert(data.Length % 8 == 0, "Decrypt payload is not a multiple of 8 bytes long.");

        if (data.Length < InternalCpuThreshold)
        {
            // Decrypt small blocks of data on the CPU, even though we have buffers set up already.
            _blowfish.DecryptStandard(data);
            return;
        }

        var dataEx = new Span<uint2>(Unsafe.AsPointer(ref data[0]), data.Length / 8);
        for (var i = 0; i < dataEx.Length; i += _gpuUpload.Span.Length)
        {
            // Calculate the length of the data to decrypt
            var len = Math.Min(_gpuUpload.Span.Length, dataEx[i..].Length);
            var dataSlice = dataEx.Slice(i, len);

            // Copy that data into the upload buffer
            dataSlice.CopyTo(_gpuUpload.Span);

            // Copy data from the upload buffer into the work buffer
            _gpuUpload.CopyTo(_gpuBuffer);

            // Run the compute shader
            GraphicsDevice.Default.For(_gpuUpload.Span.Length, 1, 1, 8, 8, 1,
                new BlowfishShader(_boxes.S0, _boxes.S1, _boxes.S2, _boxes.S3, _boxes.P, _gpuBuffer));

            // Copy data from the work buffer into the readback buffer
            _gpuDownload.CopyFrom(_gpuBuffer);

            // Copy data from the readback buffer into main memory
            _gpuDownload.Span[..len].CopyTo(dataSlice);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _gpuUpload.Dispose();
            _gpuBuffer.Dispose();
            _gpuDownload.Dispose();
            _boxes.Dispose();
        }
    }
}