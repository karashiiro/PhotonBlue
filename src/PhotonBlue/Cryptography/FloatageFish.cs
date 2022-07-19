namespace PhotonBlue.Cryptography;

public class FloatageFish
{
    public static byte DecryptByte(byte data, uint key, int shift)
    {
        var xorByte = (byte)(((key >> shift) ^ key) & 0xFF);
        if (data != 0 && data != xorByte)
        {
            return (byte)(data ^ xorByte);
        }

        return data;
    }
    
    public static void DecryptBlock(byte[] dataBlock, uint offset, uint length, uint key, int shift)
    {
        var xorByte = (byte)(((key >> shift) ^ key) & 0xFF);
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
}