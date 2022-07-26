namespace PhotonBlue.Cryptography;

public class BlowfishCpuStrategy : BlowfishStrategy
{
    private readonly Blowfish _blowfish;

    public BlowfishCpuStrategy(IEnumerable<byte> key)
    {
        _blowfish = new Blowfish(key);
    }
    
    public override void Decrypt(Span<byte> data)
    {
        _blowfish.DecryptStandard(data);
    }
}