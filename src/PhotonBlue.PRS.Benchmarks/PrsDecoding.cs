using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace PhotonBlue.PRS.Benchmarks;

[SimpleJob(RuntimeMoniker.Net60)]
[MemoryDiagnoser]
public class PrsDecoding
{
    private byte[] _decompressedData = null!;
    private byte[] _compressedData = null!;

    private PrsDecoderStream _prsStream = null!;
    private byte[] _prsBuffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _decompressedData = DataLoader.LoadDecompressed();
        _compressedData = DataLoader.LoadCompressed();
    }

    [IterationSetup(Target = "PrsDecoderStream")]
    public void PrsDecoderStream_IterationSetup()
    {
        _prsStream = new PrsDecoderStream(new MemoryStream(_compressedData));
        _prsBuffer = new byte[_decompressedData.Length];
    }

    [IterationSetup(Target = "PrsDecoderStream_Buffered")]
    public void PrsDecoderStream_Buffered_IterationSetup()
    {
        var bufferedStream = new BufferedStream(new MemoryStream(_compressedData));
        _prsStream = new PrsDecoderStream(bufferedStream);
        _prsBuffer = new byte[_decompressedData.Length];
    }

    [Benchmark]
    public int PrsDecoderStream() => _prsStream.Read(_prsBuffer);

    [Benchmark]
    public int PrsDecoderStream_Buffered() => _prsStream.Read(_prsBuffer);
}