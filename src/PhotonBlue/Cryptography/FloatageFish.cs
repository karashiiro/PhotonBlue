using System.Runtime.CompilerServices;

namespace PhotonBlue.Cryptography;

public class FloatageFish
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
    
    public static void DecryptBlock(byte[] dataBlock, uint offset, uint length, uint blowfishKey, int shift)
    {
        var xorByte = CalculateKey(blowfishKey, shift);
        for (var i = offset; i < length; ++i)
        {
            if (dataBlock[i] != 0 && dataBlock[i] != xorByte)
            {
                dataBlock[i] = (byte)(dataBlock[i] ^ xorByte);
            }
            else
            {
                dataBlock[i] = dataBlock[i];
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DecryptBlockWithKey(byte[] dataBlock, uint offset, uint length, byte key)
    {
        for (var i = offset; i < length; ++i)
        {
            if (dataBlock[i] != 0 && dataBlock[i] != key)
            {
                dataBlock[i] = (byte)(dataBlock[i] ^ key);
            }
            else
            {
                dataBlock[i] = dataBlock[i];
            }
        }
    }
}