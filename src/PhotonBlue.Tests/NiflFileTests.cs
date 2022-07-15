using System.IO;
using PhotonBlue.Data.Files;
using Xunit;

namespace PhotonBlue.Tests;

public class NiflFileTests
{
    [Fact]
    public void NiflFile_Parses_Headers()
    {
        using var data = File.OpenRead(@"..\..\..\..\..\testdata\tut_006353.text");
        var nifl = new NiflFile(data);
        nifl.LoadFile();
        Assert.Equal(0x4c46494eU, nifl.Header.Magic);
        Assert.Equal(0x304c4552U, nifl.Rel0.Magic);
        Assert.Equal(0x30464f4eU, nifl.Nof0.Magic);
    }
    
    [Fact]
    public void NiflFile_Parses_Text()
    {
        using var data = File.OpenRead(@"..\..\..\..\..\testdata\tut_006353.text");
        var nifl = new NiflFile(data);
        nifl.LoadFile();

        var text = nifl.ReadText();
        Assert.NotEmpty(text);
        Assert.All(text, Assert.NotEmpty);
    }
}