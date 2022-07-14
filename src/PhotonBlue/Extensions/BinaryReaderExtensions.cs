namespace PhotonBlue.Extensions;

public static class BinaryReaderExtensions
{
    public static void Seek(this BinaryReader reader, long offset, SeekOrigin origin)
    {
        reader.BaseStream.Seek(offset, origin);
    }
}