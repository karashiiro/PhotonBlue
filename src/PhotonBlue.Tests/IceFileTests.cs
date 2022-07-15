using System.IO;
using PhotonBlue.Data.Files;
using Xunit;

namespace PhotonBlue.Tests;

public class IceFileTests
{
    [Fact]
    public void IceFile_Parses_V4_Data()
    {
        using var data = File.OpenRead(@"..\..\..\..\..\testdata\b568a6a6c428485a57b67a1da466de");
        var ice = new IceFileV4(data);
        ice.LoadFile();
        
        Assert.Equal(1, ice.Group1Entries.Count);
        Assert.Equal(3, ice.Group2Entries.Count);
    }
}