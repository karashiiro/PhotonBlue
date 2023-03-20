namespace PhotonBlue.PRS.Tests;

public class DataLoader
{
    public static byte[] LoadCompressed()
    {
        // Compressed with https://github.com/HybridEidolon/rust-ages-prs
        // using the modern PRS compression algorithm
        return File.ReadAllBytes("output.prs");
    }

    public static byte[] LoadDecompressed()
    {
        return File.ReadAllBytes("input.txt");
    }

    public static byte[] LoadCompressedSmall()
    {
        // Compressed with https://github.com/HybridEidolon/rust-ages-prs
        // using the modern PRS compression algorithm
        return File.ReadAllBytes("output_small.prs");
    }

    public static byte[] LoadDecompressedSmall()
    {
        return File.ReadAllBytes("input_small.txt");
    }
}