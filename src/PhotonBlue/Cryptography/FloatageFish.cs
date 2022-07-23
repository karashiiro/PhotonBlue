using System.Runtime.CompilerServices;

namespace PhotonBlue.Cryptography;

internal sealed class FloatageFish
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte CalculateKey(uint blowfishKey, int shift)
    {
        return (byte)(((blowfishKey >> shift) ^ blowfishKey) & 0xFF);
    }
    
    public static byte DecryptByte(byte data, uint blowfishKey, int shift)
    {
        var xorByte = CalculateKey(blowfishKey, shift);
        if (data != 0 && data != xorByte)
        {
            return (byte)(data ^ xorByte);
        }

        return data;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte DecryptByteWithKey(byte data, byte key)
    {
        if (data != 0 && data != key)
        {
            return (byte)(data ^ key);
        }

        return data;
    }
    
    public static void DecryptBlock(byte[] dataBlock, int offset, int length, uint blowfishKey, int shift)
    {
        var xorByte = CalculateKey(blowfishKey, shift);
        var block = dataBlock.AsSpan(offset, length);
        for (var i = 0; i < block.Length; i++)
        {
            if (block[i] != 0 && block[i] != xorByte)
            {
                block[i] = (byte)(block[i] ^ xorByte);
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DecryptBlockWithKey(byte[] dataBlock, int offset, int length, byte key)
    {
        var block = dataBlock.AsSpan(offset, length);
        for (var i = 0; i < block.Length; i++)
        {
            if (block[i] != 0 && block[i] != key)
            {
                block[i] = (byte)(block[i] ^ key);
            }
        }
    }
}