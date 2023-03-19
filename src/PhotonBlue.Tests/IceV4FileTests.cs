using System.Collections.Generic;
using System.IO;
using System.Linq;
using PhotonBlue.Cryptography;
using PhotonBlue.Data.Files;
using Xunit;

namespace PhotonBlue.Tests;

public class IceV4FileTests
{
    [Fact]
    public void Should_Parse_Kraken_Data_1()
    {
        using var blowfishGpuPool = new BlowfishGpuBufferPool();
        using var data = File.OpenRead(@"..\..\..\..\..\testdata\b568a6a6c428485a57b67a1da466de");
        var ice = new IceV4File(data, blowfishGpuPool);
        ice.LoadFile();

        Assert.Equal(1, ice.Group1Entries.Count);
        Assert.All(ice.Group1Entries, AssertEntryValid);

        Assert.Equal(3, ice.Group2Entries.Count);
        Assert.All(ice.Group2Entries, AssertEntryValid);

        var expected1 = File.ReadAllBytes(@"..\..\..\..\..\testdata\tut_006353.skit");
        var actual1 = ice.Group1Entries.First(entry => entry.Header.FileName == "tut_006353.skit");
        AssertDataEquivalent(expected1, actual1.Data);

        var expected2 = File.ReadAllBytes(@"..\..\..\..\..\testdata\tut_006353.text");
        var actual2 = ice.Group2Entries.First(entry => entry.Header.FileName == "tut_006353.text");
        AssertDataEquivalent(expected2, actual2.Data);

        var expected3 = File.ReadAllBytes(@"..\..\..\..\..\testdata\ui_tut_6353_0001.dds");
        var actual3 = ice.Group2Entries.First(entry => entry.Header.FileName == "ui_tut_6353_0001.dds");
        AssertDataEquivalent(expected3, actual3.Data);

        var expected4 = File.ReadAllBytes(@"..\..\..\..\..\testdata\ui_tut_6353_0002.dds");
        var actual4 = ice.Group2Entries.First(entry => entry.Header.FileName == "ui_tut_6353_0002.dds");
        AssertDataEquivalent(expected4, actual4.Data);
    }

    [Fact]
    public void Should_Parse_Kraken_Data_2()
    {
        using var blowfishGpuPool = new BlowfishGpuBufferPool();
        using var data = File.OpenRead(@"..\..\..\..\..\testdata\0000064b91444b04df5d95f6a0bc55be");
        var ice = new IceV4File(data, blowfishGpuPool);
        ice.LoadFile();

        Assert.Equal(0, ice.Group1Entries.Count);
        Assert.Equal(5, ice.Group2Entries.Count);
        Assert.All(ice.Group2Entries, AssertEntryValid);

        var expected1 = File.ReadAllBytes(@"..\..\..\..\..\testdata\pl_rba_500841_d.dds");
        var actual1 = ice.Group2Entries.First(entry => entry.Header.FileName == "pl_rba_500841_d.dds");
        AssertDataEquivalent(expected1, actual1.Data);

        var expected2 = File.ReadAllBytes(@"..\..\..\..\..\testdata\pl_rba_500841_l.dds");
        var actual2 = ice.Group2Entries.First(entry => entry.Header.FileName == "pl_rba_500841_l.dds");
        AssertDataEquivalent(expected2, actual2.Data);

        var expected3 = File.ReadAllBytes(@"..\..\..\..\..\testdata\pl_rba_500841_m.dds");
        var actual3 = ice.Group2Entries.First(entry => entry.Header.FileName == "pl_rba_500841_m.dds");
        AssertDataEquivalent(expected3, actual3.Data);

        var expected4 = File.ReadAllBytes(@"..\..\..\..\..\testdata\pl_rba_500841_n.dds");
        var actual4 = ice.Group2Entries.First(entry => entry.Header.FileName == "pl_rba_500841_n.dds");
        AssertDataEquivalent(expected4, actual4.Data);

        var expected5 = File.ReadAllBytes(@"..\..\..\..\..\testdata\pl_rba_500841_s.dds");
        var actual5 = ice.Group2Entries.First(entry => entry.Header.FileName == "pl_rba_500841_s.dds");
        AssertDataEquivalent(expected5, actual5.Data);
    }

    [Fact]
    public void Should_Parse_Encrypted_Kraken_Data()
    {
        using var blowfishGpuPool = new BlowfishGpuBufferPool();
        using var data = File.OpenRead(@"..\..\..\..\..\testdata\74cdd2b68f9614e70dd0b67a80e4d723");
        var ice = new IceV4File(data, blowfishGpuPool);
        ice.LoadFile();

        Assert.Equal(0, ice.Group1Entries.Count);
        Assert.Equal(2, ice.Group2Entries.Count);
        Assert.All(ice.Group2Entries, AssertEntryValid);

        var expected1 = File.ReadAllBytes(@"..\..\..\..\..\testdata\mainmenu_banner_05.dds");
        var actual1 = ice.Group2Entries.First(entry => entry.Header.FileName == "mainmenu_banner_05.dds");
        AssertDataEquivalent(expected1, actual1.Data);

        var expected2 = File.ReadAllBytes(@"..\..\..\..\..\testdata\mainmenu_banner_05.text");
        var actual2 = ice.Group2Entries.First(entry => entry.Header.FileName == "mainmenu_banner_05.text");
        AssertDataEquivalent(expected2, actual2.Data);
    }

    [Fact]
    public void Should_Parse_PRS_Data()
    {
        using var blowfishGpuPool = new BlowfishGpuBufferPool();
        using var data = File.OpenRead(@"..\..\..\..\..\testdata\cb1001342c1f786545795140c345f1");
        var ice = new IceV4File(data, blowfishGpuPool);
        ice.LoadFile();

        Assert.Equal(0, ice.Group1Entries.Count);
        Assert.Equal(31, ice.Group2Entries.Count);
        Assert.All(ice.Group2Entries, AssertEntryValid);
    }

    [Fact]
    public void Should_Parse_Encrypted_PRS_Data_1()
    {
        using var blowfishGpuPool = new BlowfishGpuBufferPool();
        using var data = File.OpenRead(@"..\..\..\..\..\testdata\000fbf4e4b152c9970398f4c82012b95");
        var ice = new IceV4File(data, blowfishGpuPool);
        ice.LoadFile();

        Assert.Equal(0, ice.Group1Entries.Count);
        Assert.Equal(1, ice.Group2Entries.Count);
        Assert.All(ice.Group2Entries, AssertEntryValid);

        var expected = File.ReadAllBytes(@"..\..\..\..\..\testdata\ui_making_decoy01_26802.dds");
        var actual = ice.Group2Entries.First(entry => entry.Header.FileName == "ui_making_decoy01_26802.dds");
        AssertDataEquivalent(expected, actual.Data);
    }

    [Fact]
    public void Should_Parse_Encrypted_PRS_Data_2()
    {
        using var blowfishGpuPool = new BlowfishGpuBufferPool();
        using var data = File.OpenRead(@"..\..\..\..\..\testdata\0000ad8daf393f31da0fd7e26829c819");
        var ice = new IceV4File(data, blowfishGpuPool);
        ice.LoadFile();

        Assert.Equal(0, ice.Group1Entries.Count);
        Assert.Equal(1, ice.Group2Entries.Count);
        Assert.All(ice.Group2Entries, AssertEntryValid);

        var expected = File.ReadAllBytes(@"..\..\..\..\..\testdata\ui_item_3015835.dds");
        var actual = ice.Group2Entries.First(entry => entry.Header.FileName == "ui_item_3015835.dds");
        AssertDataEquivalent(expected, actual.Data);
    }

    [Fact]
    public void Should_Parse_Encrypted_PRS_Data_3()
    {
        using var blowfishGpuPool = new BlowfishGpuBufferPool();
        using var data = File.OpenRead(@"..\..\..\..\..\testdata\0002c97e93075ec680d89801fa640912");
        var ice = new IceV4File(data, blowfishGpuPool);
        ice.LoadFile();

        Assert.Equal(0, ice.Group1Entries.Count);
        Assert.Equal(1, ice.Group2Entries.Count);
        Assert.All(ice.Group2Entries, AssertEntryValid);

        var expected = File.ReadAllBytes(@"..\..\..\..\..\testdata\ui_making_costume02_50161.dds");
        var actual = ice.Group2Entries.First(entry => entry.Header.FileName == "ui_making_costume02_50161.dds");
        AssertDataEquivalent(expected, actual.Data);
    }

    [Fact]
    public void Should_Parse_Encrypted_PRS_Data_3_HeadersOnly()
    {
        using var blowfishGpuPool = new BlowfishGpuBufferPool();
        using var data = File.OpenRead(@"..\..\..\..\..\testdata\0002c97e93075ec680d89801fa640912");
        var ice = new IceV4File(data, blowfishGpuPool);
        ice.LoadHeadersOnly();

        Assert.Equal(0, ice.Group1Entries.Count);
        Assert.Equal(1, ice.Group2Entries.Count);
    }

    [Fact]
    public void Should_Parse_Encrypted_PRS_Data_4()
    {
        using var blowfishGpuPool = new BlowfishGpuBufferPool();
        using var data = File.OpenRead(@"..\..\..\..\..\testdata\000a686a27ade4d971ac5e27a664a5a3");
        var ice = new IceV4File(data, blowfishGpuPool);
        ice.LoadFile();

        Assert.Equal(1, ice.Group1Entries.Count);
        Assert.All(ice.Group1Entries, AssertEntryValid);

        Assert.Equal(37, ice.Group2Entries.Count);
        Assert.All(ice.Group2Entries, AssertEntryValid);
    }

    [Fact]
    public void Should_Parse_Encrypted_PRS_Data_5()
    {
        using var blowfishGpuPool = new BlowfishGpuBufferPool();
        using var data = File.OpenRead(@"..\..\..\..\..\testdata\00150669267df8e4fdfd58cda0c1b9a0");
        var ice = new IceV4File(data, blowfishGpuPool);
        ice.LoadFile();

        Assert.Equal(1, ice.Group1Entries.Count);
        Assert.All(ice.Group1Entries, AssertEntryValid);

        Assert.Equal(37, ice.Group2Entries.Count);
        Assert.All(ice.Group2Entries, AssertEntryValid);

        var expected1 = File.ReadAllBytes(@"..\..\..\..\..\testdata\weapon_specific.ini.lua");
        var actual1 = ice.Group1Entries.First(entry => entry.Header.FileName == "weapon_specific.ini.lua");
        AssertDataEquivalent(expected1, actual1.Data);

        var expected2 = File.ReadAllBytes(@"..\..\..\..\..\testdata\05_item_rifle_10077.acb");
        var actual2 = ice.Group2Entries.First(entry => entry.Header.FileName == "05_item_rifle_10077.acb");
        AssertDataEquivalent(expected2, actual2.Data);
    }

    [Fact]
    public void Should_Parse_Encrypted_PRS_Data_5_HeadersOnly()
    {
        using var blowfishGpuPool = new BlowfishGpuBufferPool();
        using var data = File.OpenRead(@"..\..\..\..\..\testdata\00150669267df8e4fdfd58cda0c1b9a0");
        var ice = new IceV4File(data, blowfishGpuPool);
        ice.LoadHeadersOnly();

        Assert.Equal(1, ice.Group1Entries.Count);
        Assert.Equal(37, ice.Group2Entries.Count);
    }

    [Fact]
    public void Should_Parse_Encrypted_PRS_Data_6()
    {
        using var blowfishGpuPool = new BlowfishGpuBufferPool();
        using var data = File.OpenRead(@"..\..\..\..\..\testdata\000f4dde50137d18a3f595863423a93c");
        var ice = new IceV4File(data, blowfishGpuPool);
        ice.LoadFile();

        Assert.Equal(1, ice.Group1Entries.Count);
        Assert.All(ice.Group1Entries, AssertEntryValid);

        Assert.Equal(0, ice.Group2Entries.Count);

        var expected1 = File.ReadAllBytes(@"..\..\..\..\..\testdata\np_npc_536.cml");
        var actual1 = ice.Group1Entries.First(entry => entry.Header.FileName == "np_npc_536.cml");
        AssertDataEquivalent(expected1, actual1.Data);
    }

    [Fact]
    public void Should_Parse_Encrypted_PRS_Data_6_HeadersOnly()
    {
        using var blowfishGpuPool = new BlowfishGpuBufferPool();
        using var data = File.OpenRead(@"..\..\..\..\..\testdata\000f4dde50137d18a3f595863423a93c");
        var ice = new IceV4File(data, blowfishGpuPool);
        ice.LoadHeadersOnly();

        Assert.Equal(1, ice.Group1Entries.Count);
        Assert.Equal(0, ice.Group2Entries.Count);
    }

    [Fact]
    public void Should_Parse_Encrypted_Uncompressed_Data()
    {
        using var blowfishGpuPool = new BlowfishGpuBufferPool();
        using var data = File.OpenRead(@"..\..\..\..\..\testdata\0006b03a4c2763ffcd7d4547f71600dd");
        var ice = new IceV4File(data, blowfishGpuPool);
        ice.LoadFile();

        Assert.Equal(0, ice.Group1Entries.Count);
        Assert.Equal(2, ice.Group2Entries.Count);
        Assert.All(ice.Group2Entries, AssertEntryValid);

        var expected1 = File.ReadAllBytes(@"..\..\..\..\..\testdata\11_sound_voice_bt_npc_5180.acb");
        var actual1 = ice.Group2Entries.First(entry => entry.Header.FileName == "11_sound_voice_bt_npc_5180.acb");
        AssertDataEquivalent(expected1, actual1.Data);

        var expected2 = File.ReadAllBytes(@"..\..\..\..\..\testdata\11_sound_voice_bt_npc_5180.snd");
        var actual2 = ice.Group2Entries.First(entry => entry.Header.FileName == "11_sound_voice_bt_npc_5180.snd");
        AssertDataEquivalent(expected2, actual2.Data);
    }

    [Fact]
    public void Should_Parse_Encrypted_Uncompressed_Data_HeadersOnly()
    {
        using var blowfishGpuPool = new BlowfishGpuBufferPool();
        using var data = File.OpenRead(@"..\..\..\..\..\testdata\0006b03a4c2763ffcd7d4547f71600dd");
        var ice = new IceV4File(data, blowfishGpuPool);
        ice.LoadHeadersOnly();

        Assert.Equal(0, ice.Group1Entries.Count);
        Assert.Equal(2, ice.Group2Entries.Count);
    }

    private static void AssertDataEquivalent(IReadOnlyCollection<byte> expected, IReadOnlyCollection<byte> actual)
    {
        Assert.Equal(expected.AsEnumerable(), actual.AsEnumerable());
        if (expected.Count <= actual.Count)
        {
            Assert.Equal(expected.AsEnumerable(), actual.AsEnumerable());
        }
        else
        {
            // See https://github.com/Shadowth117/Zamboni/issues/1 - Zamboni's outputs can have extra padding
            Assert.Equal(expected.AsEnumerable().Take(actual.Count), actual.AsEnumerable());
            Assert.All(expected.AsEnumerable().Skip(actual.Count), b => Assert.Equal(0, b));
        }
    }

    private static void AssertEntryValid(IceFile.FileEntry entry)
    {
        Assert.True(entry.Header.FileNameRaw.Length > 0);
        Assert.True(entry.Header.DataSize == entry.Data.Length);
    }
}