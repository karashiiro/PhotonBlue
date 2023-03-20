using BenchmarkDotNet.Attributes;

namespace PhotonBlue.PRS.Benchmarks;

public partial class PrsDecoding
{
    [IterationSetup(Target = "DecoderStream_Read")]
    public void DecoderStream_Read_IterationSetup()
    {
        _prsStream = new PrsDecoderStream(new MemoryStream(_compressedData));
        _prsBuffer = new byte[_decompressedData.Length];
    }

    [IterationSetup(Target = "DecoderStream_Seek")]
    public void DecoderStream_Seek_IterationSetup()
    {
        _prsStream = new PrsDecoderStream(new MemoryStream(_compressedData));
        _prsBuffer = new byte[_decompressedData.Length];
    }

    [IterationSetup(Target = "OneShotDecoder")]
    public void OneShotDecoder_IterationSetup()
    {
        _prsBuffer = new byte[_decompressedData.Length];
    }
}