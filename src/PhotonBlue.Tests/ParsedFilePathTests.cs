using PhotonBlue.Data;
using Xunit;

namespace PhotonBlue.Tests;

public class ParsedFilePathTests
{
    [Fact]
    public void Should_Parse()
    {
        var path = ParsedFilePath.ParseFilePath("win32/00000000000000000000000000000000/wp_00_00_00_00_00_r.aqp");
        Assert.NotNull(path);
        Assert.Equal("wp_00_00_00_00_00_r.aqp", path.FileName);
        Assert.Equal("00000000000000000000000000000000", path.PackName);
        Assert.Equal("win32", path.ResourceFolder);
        Assert.Equal("win32/00000000000000000000000000000000/wp_00_00_00_00_00_r.aqp", path.RawPath);
    }
}