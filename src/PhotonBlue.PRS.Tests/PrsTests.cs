using System.Text;

namespace PhotonBlue.PRS.Tests;

public class PrsTests
{
    private readonly byte[] _decompressedData = DataLoader.LoadDecompressed();
    private readonly byte[] _compressedData = DataLoader.LoadCompressed();
    private readonly byte[] _decompressedSmallData = DataLoader.LoadDecompressedSmall();
    private readonly byte[] _compressedSmallData = DataLoader.LoadCompressedSmall();

    [Fact]
    public void PrsDecoderStream_Decoding_Works()
    {
        using var compressed = new MemoryStream(_compressedData);

        var actual = new byte[_decompressedData.Length];
        var prs = new PrsDecoderStream(compressed);

        // ReSharper disable once MustUseReturnValue
        prs.Read(actual);

        CheckResult(_decompressedData, actual);
    }

    [Fact]
    public void PrsDecoderStream_Seeking_Works()
    {
        using var compressed = new MemoryStream(_compressedData);
        var prs = new PrsDecoderStream(compressed);
        var newPos = prs.Seek(_decompressedData.Length, SeekOrigin.Current);
        Assert.Equal(newPos, _decompressedData.Length);
    }

    [Fact]
    public void PrsDecoderStream_Decoding_Works_Small()
    {
        using var compressed = new MemoryStream(_compressedSmallData);

        var actual = new byte[_decompressedSmallData.Length];
        var prs = new PrsDecoderStream(compressed);

        // ReSharper disable once MustUseReturnValue
        prs.Read(actual);

        CheckResult(_decompressedSmallData, actual);
    }

    [Fact]
    public void PrsDecoderStream_Seeking_Works_Small()
    {
        using var compressed = new MemoryStream(_compressedSmallData);
        var prs = new PrsDecoderStream(compressed);
        var newPos = prs.Seek(_decompressedSmallData.Length, SeekOrigin.Current);
        Assert.Equal(newPos, _decompressedSmallData.Length);
    }

    [Fact]
    public void PrsOneShotDecoder_Decoding_Works()
    {
        for (var i = 0; i < 1000; i++)
        {
            var actual = new byte[_decompressedData.Length];
            PrsOneShotDecoder.Decode(_compressedData, actual);
            CheckResult(_decompressedData, actual);
        }
    }

    [Fact]
    public void PrsOneShotDecoder_Decoding_Works_Small()
    {
        for (var i = 0; i < 1000; i++)
        {
            var actual = new byte[_decompressedSmallData.Length];
            PrsOneShotDecoder.Decode(_compressedSmallData, actual);
            CheckResult(_decompressedSmallData, actual);
        }
    }

    [Fact]
    public void PrsOneShotDecoder_AlignRangesOverCut_Works_BothWrapped()
    {
        Span<int> source = stackalloc int[8];
        source[0] = 8188;
        source[1] = 8195;

        Span<int> destination = stackalloc int[8];
        destination[0] = 8186;
        destination[1] = 8193;

        Span<int> expected1 = stackalloc int[8];
        expected1[0] = 8188;
        expected1[1] = 8190;
        expected1[2] = 8191;
        expected1[3] = 8192;
        expected1[4] = 8193;
        expected1[5] = 8195;
        expected1[6] = 0;
        expected1[7] = 0;

        Span<int> expected2 = stackalloc int[8];
        expected2[0] = 8186;
        expected2[1] = 8188;
        expected2[2] = 8189;
        expected2[3] = 8190;
        expected2[4] = 8191;
        expected2[5] = 8193;
        expected2[6] = 0;
        expected2[7] = 0;

        InclusiveRangeUtils.AlignRangesOverCut(source, destination, 8191);

        Assert.Equal(expected1.ToArray(), source.ToArray());
        Assert.Equal(expected2.ToArray(), destination.ToArray());
    }

    [Fact]
    public void PrsOneShotDecoder_AlignRangesOverCut_Works_SourceWrapped()
    {
        Span<int> source = stackalloc int[8];
        source[0] = 8188;
        source[1] = 8195;

        Span<int> destination = stackalloc int[8];
        destination[0] = 8180;
        destination[1] = 8187;

        Span<int> expected1 = stackalloc int[8];
        expected1[0] = 8188;
        expected1[1] = 8190;
        expected1[2] = 8191;
        expected1[3] = 8195;
        expected1[4] = 0;
        expected1[5] = 0;
        expected1[6] = 0;
        expected1[7] = 0;

        Span<int> expected2 = stackalloc int[8];
        expected2[0] = 8180;
        expected2[1] = 8182;
        expected2[2] = 8183;
        expected2[3] = 8187;
        expected2[4] = 0;
        expected2[5] = 0;
        expected2[6] = 0;
        expected2[7] = 0;

        InclusiveRangeUtils.AlignRangesOverCut(source, destination, 8191);

        Assert.Equal(expected1.ToArray(), source.ToArray());
        Assert.Equal(expected2.ToArray(), destination.ToArray());
    }

    [Fact]
    public void PrsOneShotDecoder_AlignRangesOverCut_Works_DestinationWrapped()
    {
        Span<int> source = stackalloc int[8];
        source[0] = 8180;
        source[1] = 8187;

        Span<int> destination = stackalloc int[8];
        destination[0] = 8188;
        destination[1] = 8195;

        Span<int> expected1 = stackalloc int[8];
        expected1[0] = 8180;
        expected1[1] = 8182;
        expected1[2] = 8183;
        expected1[3] = 8187;
        expected1[4] = 0;
        expected1[5] = 0;
        expected1[6] = 0;
        expected1[7] = 0;

        Span<int> expected2 = stackalloc int[8];
        expected2[0] = 8188;
        expected2[1] = 8190;
        expected2[2] = 8191;
        expected2[3] = 8195;
        expected2[4] = 0;
        expected2[5] = 0;
        expected2[6] = 0;
        expected2[7] = 0;

        InclusiveRangeUtils.AlignRangesOverCut(source, destination, 8191);

        Assert.Equal(expected1.ToArray(), source.ToArray());
        Assert.Equal(expected2.ToArray(), destination.ToArray());
    }

    [Fact]
    public void PrsOneShotDecoder_AlignRangesOverCut_Works_SourceWrapped_AtEdge()
    {
        Span<int> source = stackalloc int[8];
        source[0] = 8190;
        source[1] = 8192;

        Span<int> destination = stackalloc int[8];
        destination[0] = 7787;
        destination[1] = 7789;

        Span<int> expected1 = stackalloc int[8];
        expected1[0] = 8190;
        expected1[1] = 8190;
        expected1[2] = 8191;
        expected1[3] = 8192;
        expected1[4] = 0;
        expected1[5] = 0;
        expected1[6] = 0;
        expected1[7] = 0;

        Span<int> expected2 = stackalloc int[8];
        expected2[0] = 7787;
        expected2[1] = 7787;
        expected2[2] = 7788;
        expected2[3] = 7789;
        expected2[4] = 0;
        expected2[5] = 0;
        expected2[6] = 0;
        expected2[7] = 0;

        InclusiveRangeUtils.AlignRangesOverCut(source, destination, 8191);

        Assert.Equal(expected1.ToArray(), source.ToArray());
        Assert.Equal(expected2.ToArray(), destination.ToArray());
    }

    [Fact]
    public void PrsOneShotDecoder_AlignRangesOverCut_Works_DestinationWrapped_AtEdge()
    {
        Span<int> source = stackalloc int[8];
        source[0] = 7787;
        source[1] = 7789;

        Span<int> destination = stackalloc int[8];
        destination[0] = 8190;
        destination[1] = 8192;

        Span<int> expected1 = stackalloc int[8];
        expected1[0] = 7787;
        expected1[1] = 7787;
        expected1[2] = 7788;
        expected1[3] = 7789;
        expected1[4] = 0;
        expected1[5] = 0;
        expected1[6] = 0;
        expected1[7] = 0;

        Span<int> expected2 = stackalloc int[8];
        expected2[0] = 8190;
        expected2[1] = 8190;
        expected2[2] = 8191;
        expected2[3] = 8192;
        expected2[4] = 0;
        expected2[5] = 0;
        expected2[6] = 0;
        expected2[7] = 0;

        InclusiveRangeUtils.AlignRangesOverCut(source, destination, 8191);

        Assert.Equal(expected1.ToArray(), source.ToArray());
        Assert.Equal(expected2.ToArray(), destination.ToArray());
    }

    [Fact]
    public void PrsOneShotDecoder_AlignRangesOverCut_CutsBeforePosition()
    {
        Span<int> source = stackalloc int[8];
        source[0] = 7562;
        source[1] = 7569;

        Span<int> destination = stackalloc int[8];
        destination[0] = 8187;
        destination[1] = 8194;

        Span<int> expected1 = stackalloc int[8];
        expected1[0] = 7562;
        expected1[1] = 7565;
        expected1[2] = 7566;
        expected1[3] = 7569;
        expected1[4] = 0;
        expected1[5] = 0;
        expected1[6] = 0;
        expected1[7] = 0;

        Span<int> expected2 = stackalloc int[8];
        expected2[0] = 8187;
        expected2[1] = 8190;
        expected2[2] = 8191;
        expected2[3] = 8194;
        expected2[4] = 0;
        expected2[5] = 0;
        expected2[6] = 0;
        expected2[7] = 0;

        InclusiveRangeUtils.AlignRangesOverCut(source, destination, 8191);

        Assert.Equal(expected1.ToArray(), source.ToArray());
        Assert.Equal(expected2.ToArray(), destination.ToArray());
    }

    [Fact]
    public void PrsOneShotDecoder_AlignRangesOverCut_Works_Intersecting_NeitherWrapped()
    {
        Span<int> source = stackalloc int[8];
        source[0] = 1300;
        source[1] = 1310;

        Span<int> destination = stackalloc int[8];
        destination[0] = 1310;
        destination[1] = 1320;

        Span<int> expected1 = stackalloc int[8];
        expected1[0] = 1300;
        expected1[1] = 1300;
        expected1[2] = 1301;
        expected1[3] = 1309;
        expected1[4] = 1310;
        expected1[5] = 1310;
        expected1[6] = 0;
        expected1[7] = 0;

        Span<int> expected2 = stackalloc int[8];
        expected2[0] = 1310;
        expected2[1] = 1310;
        expected2[2] = 1311;
        expected2[3] = 1319;
        expected2[4] = 1320;
        expected2[5] = 1320;
        expected2[6] = 0;
        expected2[7] = 0;

        InclusiveRangeUtils.AlignRangesOverCut(source, destination, 8191);

        Assert.Equal(expected1.ToArray(), source.ToArray());
        Assert.Equal(expected2.ToArray(), destination.ToArray());
    }

    private static void CheckResult(byte[] expected, byte[] actual)
    {
        // Compare the string representations of the data for
        // debugging purposes since it gives a more comprehensible
        // error message
        var expectedStr = Encoding.UTF8.GetString(expected);
        var actualStr = Encoding.UTF8.GetString(actual);
        Assert.Equal(expectedStr, actualStr);

        // Compare the raw data
        Assert.Equal(expected, actual);
    }
}