namespace PhotonBlue.PRS.Benchmarks;

public class DataLoader
{
    public static byte[] LoadCompressed()
    {
        // Compressed with https://github.com/HybridEidolon/rust-ages-prs
        // using the modern PRS compression algorithm. The largest PRS
        // ICE file seems to be about 58MB (d5898e457df61b05ae21b02ac1fdbe3f),
        // so I'm benchmarking against roughly that.
        return File.ReadAllBytes("output_large.prs");
    }

    public static byte[] LoadDecompressed()
    {
        return File.ReadAllBytes("input_large.txt");
    }
}