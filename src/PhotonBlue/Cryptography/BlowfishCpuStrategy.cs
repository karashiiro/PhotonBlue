namespace PhotonBlue.Cryptography;

internal class BlowfishCpuStrategy : BlowfishStrategy
{
    private readonly Blowfish _blowfish;

    public BlowfishCpuStrategy(Blowfish blowfish)
    {
        _blowfish = blowfish;
    }
    
    public override void Decrypt(Span<byte> data)
    {
        _blowfish.DecryptStandard(data);
    }
}