using System.Diagnostics;
using System.Runtime.CompilerServices;
using ComputeSharp;

namespace PhotonBlue.Cryptography;

public class BlowfishGpuStrategy : BlowfishStrategy
{
    private readonly UploadBuffer<uint2> _gpuUpload;
    private readonly ReadWriteBuffer<uint2> _gpuBuffer;
    private readonly Blowfish.GpuHandle _boxes;

    public BlowfishGpuStrategy(IEnumerable<byte> key, int bufferSize)
    {
        var blowfish = new Blowfish(key);
        
        _boxes = blowfish.AllocateToGraphicsDevice(GraphicsDevice.Default);
        _gpuBuffer = GraphicsDevice.Default.AllocateReadWriteBuffer<uint2>(bufferSize / 8);
        _gpuUpload = GraphicsDevice.Default.AllocateUploadBuffer<uint2>(bufferSize / 8);
    }
    
    public override unsafe void Decrypt(Span<byte> data)
    {
        Debug.Assert(data.Length % 8 == 0, "Decrypt payload is not a multiple of 8 bytes long.");
        
        var dataEx = new Span<uint2>(Unsafe.AsPointer(ref data[0]), data.Length / 8);
        for (var i = 0; i < dataEx.Length; i += _gpuUpload.Span.Length)
        {
            var len = Math.Min(_gpuUpload.Span.Length, dataEx[i..].Length);
            var dataSlice = dataEx.Slice(i, len);
            dataSlice.CopyTo(_gpuUpload.Span);
            _gpuUpload.CopyTo(_gpuBuffer);
            GraphicsDevice.Default.For(_gpuUpload.Span.Length, 1, 1, 8, 8, 1,
                new BlowfishShader(_boxes.S0, _boxes.S1, _boxes.S2, _boxes.S3, _boxes.P, _gpuBuffer));
            _gpuBuffer.CopyTo(dataSlice);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        
        if (disposing)
        {
            _gpuUpload.Dispose();
            _gpuBuffer.Dispose();
            _boxes.Dispose();
        }
    }
}