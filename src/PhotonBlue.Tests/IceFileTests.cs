using System.IO;
using PhotonBlue.Data.Files;
using Xunit;

namespace PhotonBlue.Tests;

public class IceFileTests
{
    [Fact]
    public void IceFile_Parses_V4_Kraken_Data_1()
    {
        using var data = File.OpenRead(@"..\..\..\..\..\testdata\b568a6a6c428485a57b67a1da466de");
        var ice = new IceFileV4(data);
        ice.LoadFile();
        
        Assert.Equal(1, ice.Group1Entries.Count);
        Assert.All(ice.Group1Entries, AssertEntryValid);
        
        Assert.Equal(3, ice.Group2Entries.Count);
        Assert.All(ice.Group2Entries, AssertEntryValid);
    }
    
    [Fact]
    public void IceFile_Parses_V4_Kraken_Data_2()
    {
        using var data = File.OpenRead(@"..\..\..\..\..\testdata\0000064b91444b04df5d95f6a0bc55be");
        var ice = new IceFileV4(data);
        ice.LoadFile();
        
        Assert.Equal(0, ice.Group1Entries.Count);
        Assert.Equal(5, ice.Group2Entries.Count);
        Assert.All(ice.Group2Entries, AssertEntryValid);
    }
    
    [Fact]
    public void IceFile_Parses_V4_Encrypted_Kraken_Data()
    {
        using var data = File.OpenRead(@"..\..\..\..\..\testdata\74cdd2b68f9614e70dd0b67a80e4d723");
        var ice = new IceFileV4(data);
        ice.LoadFile();
        
        Assert.Equal(0, ice.Group1Entries.Count);
        Assert.Equal(2, ice.Group2Entries.Count);
        Assert.All(ice.Group2Entries, AssertEntryValid);
    }
    
    [Fact]
    public void IceFile_Parses_V4_PRS_Data_1()
    {
        using var data = File.OpenRead(@"..\..\..\..\..\testdata\cb1001342c1f786545795140c345f1");
        var ice = new IceFileV4(data);
        ice.LoadFile();
        
        Assert.Equal(0, ice.Group1Entries.Count);
        Assert.Equal(31, ice.Group2Entries.Count);
        Assert.All(ice.Group2Entries, AssertEntryValid);
    }
    
    [Fact]
    public void IceFile_Parses_V4_PRS_Data_2()
    {
        using var data = File.OpenRead(@"..\..\..\..\..\testdata\0000ad8daf393f31da0fd7e26829c819");
        var ice = new IceFileV4(data);
        ice.LoadFile();
        
        Assert.Equal(0, ice.Group1Entries.Count);
        Assert.Equal(1, ice.Group2Entries.Count);
        Assert.All(ice.Group2Entries, AssertEntryValid);
    }
    
    [Fact]
    public void IceFile_Parses_V4_Encrypted_PRS_Data()
    {
        using var data = File.OpenRead(@"..\..\..\..\..\testdata\000fbf4e4b152c9970398f4c82012b95");
        var ice = new IceFileV4(data);
        ice.LoadFile();
        
        Assert.Equal(0, ice.Group1Entries.Count);
        Assert.Equal(1, ice.Group2Entries.Count);
        Assert.All(ice.Group2Entries, AssertEntryValid);
    }

    private static void AssertEntryValid(IceFile.FileEntry entry)
    {
        Assert.True(entry.Header.FileNameRaw.Length <= 0x20);
        Assert.True(entry.Header.DataSize == entry.Data.Length);
    }
}