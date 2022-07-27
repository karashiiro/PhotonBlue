using ComputeSharp;

namespace PhotonBlue.Cryptography;

public struct BlowfishGpuHandle : IDisposable
{
    public ReadOnlyBuffer<uint> S0;
    public ReadOnlyBuffer<uint> S1;
    public ReadOnlyBuffer<uint> S2;
    public ReadOnlyBuffer<uint> S3;
    public ConstantBuffer<uint> P;

    public readonly void Dispose()
    {
        S0.Dispose();
        S1.Dispose();
        S2.Dispose();
        S3.Dispose();
        P.Dispose();
    }
}