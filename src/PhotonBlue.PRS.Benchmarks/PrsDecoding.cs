using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace PhotonBlue.PRS.Benchmarks;

[SimpleJob(RuntimeMoniker.Net60)]
[MemoryDiagnoser]
public partial class PrsDecoding
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

    [Benchmark]
    public int DecoderStream_Read() => _prsStream.Read(_prsBuffer);

    [Benchmark]
    public long DecoderStream_Seek() => _prsStream.Seek(_decompressedData.Length, SeekOrigin.Current);

    [Benchmark]
    public void OneShotDecoder() => PrsOneShotDecoder.Decode(_compressedData, _prsBuffer);
}