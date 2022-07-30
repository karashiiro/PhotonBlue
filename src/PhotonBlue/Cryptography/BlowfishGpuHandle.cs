using ComputeSharp;

namespace PhotonBlue.Cryptography;

public struct BlowfishGpuHandle : IDisposable
{
    public UploadBuffer<uint2> Upload;
    public ReadBackBuffer<uint2> Download;
    public ConstantBuffer<uint> S0;
    public ConstantBuffer<uint> S1;
    public ConstantBuffer<uint> S2;
    public ConstantBuffer<uint> S3;
    public ConstantBuffer<uint> P;

    public ReadWriteBuffer<uint2> Data;

    public readonly void Dispose()
    {
        Upload.Dispose();
        Download.Dispose();
        S0.Dispose();
        S1.Dispose();
        S2.Dispose();
        S3.Dispose();
        P.Dispose();
        Data.Dispose();
    }
}