using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using PhotonBlue.Cryptography;
using PhotonBlue.Data.Files;

namespace PhotonBlue.IceV4Benchmark;

[Config(typeof(IceV4ParsingConfig))]
public class IceV4Parsing
{
    public class IceV4ParsingConfig : ManualConfig
    {
        public IceV4ParsingConfig()
        {
            AddJob(Job.MediumRun
                .WithLaunchCount(1)
                .WithToolchain(InProcessEmitToolchain.Instance));
        }
    }

    private const string EncKrakenFilePath = @"..\..\..\..\..\testdata\74cdd2b68f9614e70dd0b67a80e4d723";
    private const string EncPrsFilePath = @"..\..\..\..\..\testdata\00150669267df8e4fdfd58cda0c1b9a0";
    private const string KrakenFilePath = @"..\..\..\..\..\testdata\0000064b91444b04df5d95f6a0bc55be";
    private const string PrsFilePath = @"..\..\..\..\..\testdata\cb1001342c1f786545795140c345f1";

    private readonly byte[] _encryptedKrakenData;
    private readonly byte[] _encryptedPrsData;
    private readonly byte[] _krakenData;
    private readonly byte[] _prsData;

    private readonly IObjectPool<BlowfishGpuHandle, Blowfish> _blowfishGpuPool;

    public IceV4Parsing()
    {
        _encryptedKrakenData = File.ReadAllBytes(EncKrakenFilePath);
        _encryptedPrsData = File.ReadAllBytes(EncPrsFilePath);
        _krakenData = File.ReadAllBytes(KrakenFilePath);
        _prsData = File.ReadAllBytes(PrsFilePath);

        _blowfishGpuPool = new BlowfishGpuBufferPool();
    }

    [Benchmark]
    public IceV4File EncryptedKrakenCompleteFile()
    {
        using var mem = new MemoryStream(_encryptedKrakenData);
        var ice = new IceV4File(mem, _blowfishGpuPool);
        ice.LoadFile();
        return ice;
    }

    [Benchmark]
    public IceV4File EncryptedKrakenHeadersOnly()
    {
        using var mem = new MemoryStream(_encryptedKrakenData);
        var ice = new IceV4File(mem, _blowfishGpuPool);
        ice.LoadHeadersOnly();
        return ice;
    }

    [Benchmark]
    public IceV4File EncryptedPrsCompleteFile()
    {
        using var mem = new MemoryStream(_encryptedPrsData);
        var ice = new IceV4File(mem, _blowfishGpuPool);
        ice.LoadFile();
        return ice;
    }

    [Benchmark]
    public IceV4File EncryptedPrsHeadersOnly()
    {
        using var mem = new MemoryStream(_encryptedPrsData);
        var ice = new IceV4File(mem, _blowfishGpuPool);
        ice.LoadHeadersOnly();
        return ice;
    }

    [Benchmark]
    public IceV4File KrakenCompleteFile()
    {
        using var mem = new MemoryStream(_krakenData);
        var ice = new IceV4File(mem, _blowfishGpuPool);
        ice.LoadFile();
        return ice;
    }

    [Benchmark]
    public IceV4File KrakenHeadersOnly()
    {
        using var mem = new MemoryStream(_krakenData);
        var ice = new IceV4File(mem, _blowfishGpuPool);
        ice.LoadHeadersOnly();
        return ice;
    }

    [Benchmark]
    public IceV4File PrsCompleteFile()
    {
        using var mem = new MemoryStream(_prsData);
        var ice = new IceV4File(mem, _blowfishGpuPool);
        ice.LoadFile();
        return ice;
    }

    [Benchmark]
    public IceV4File PrsHeadersOnly()
    {
        using var mem = new MemoryStream(_prsData);
        var ice = new IceV4File(mem, _blowfishGpuPool);
        ice.LoadHeadersOnly();
        return ice;
    }
}