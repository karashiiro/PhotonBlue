using System.Text;

namespace PhotonBlue.PRS.Tests;

public class PrsTests
{
    private readonly byte[] _decompressedData = DataLoader.LoadDecompressed();
    private readonly byte[] _compressedData = DataLoader.LoadCompressed();

    [Fact]
    public void Decoding_Works()
    {
        using var compressed = new MemoryStream(_compressedData);

        var actual = new byte[_decompressedData.Length];
        var prs = new PrsDecoderStream(compressed);

        // ReSharper disable once MustUseReturnValue
        prs.Read(actual);

        CheckResult(actual);
    }

    private void CheckResult(byte[] actual)
    {
        // Compare the string representations of the data for
        // debugging purposes since it gives a more comprehensible
        // error message
        var expectedStr = Encoding.UTF8.GetString(_decompressedData);
        var actualStr = Encoding.UTF8.GetString(actual);
        Assert.Equal(expectedStr, actualStr);

        // Compare the raw data
        Assert.Equal(_decompressedData, actual);
    }
}