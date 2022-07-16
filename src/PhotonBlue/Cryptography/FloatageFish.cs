namespace PhotonBlue.Cryptography;

public class FloatageFish
{
    public static byte[] DecryptBlock(byte[] dataBlock, uint length, uint key, int shift)
    {
        var xorByte = (byte)(((key >> shift) ^ key) & 0xFF);
        var toReturn = new byte[length];

        for (uint i = 0; i < length; ++i)
        {
            if (dataBlock[i] != 0 && dataBlock[i] != xorByte)
                toReturn[i] = (byte)(dataBlock[i] ^ xorByte);
            else
                toReturn[i] = dataBlock[i];
        }
        
        return toReturn;
    }
}