namespace PhotonBlue.Cryptography;

public class FloatageFish
{
    public static void DecryptBlock(byte[] dataBlock, uint length, uint key, int shift)
    {
        var xorByte = (byte)(((key >> shift) ^ key) & 0xFF);
        for (uint i = 0; i < length; ++i)
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