using ComputeSharp;

namespace PhotonBlue.Cryptography;

[AutoConstructor]
[EmbeddedBytecode(8, 8, 1)]
public readonly partial struct BlowfishShader : IComputeShader
{
    private const int Rounds = 16;
    
    public readonly ConstantBuffer<uint> s0;
    public readonly ConstantBuffer<uint> s1;
    public readonly ConstantBuffer<uint> s2;
    public readonly ConstantBuffer<uint> s3;
    public readonly ConstantBuffer<uint> p;
    
    // Work buffer data is packed as a buffer of L/R pairs for simplicity.
    // The X element of the vector corresponds to the left integer in the
    // CPU implementation, and the Y element corresponds to the right one.
    public readonly ReadWriteBuffer<uint2> data;

    public void Execute()
    {
        data[ThreadIds.X] = Decrypt(data[ThreadIds.X]); 
    }

    private uint2 Decrypt(uint2 pair)
    {
        for (var i = Rounds; i > 0; i -= 2)
        {
            pair.X ^= p[i + 1];
            pair.Y ^= p[i];
            pair.Y ^= F(pair.X);
            pair.X ^= F(pair.Y);
        }
        
        pair.X ^= p[1];
        pair.Y ^= p[0];

        return pair.YX;
    }
    
    private uint F(uint x)
    {
        return ((s0[(int)(x >> 24)] + s1[(int)((x >> 16) & 0xFF)]) ^ s2[(int)((x >> 8) & 0xFF)]) + s3[(int)(x & 0xFF)];
    }
}