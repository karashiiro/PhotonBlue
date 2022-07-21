using System.Linq;
using Xunit;

namespace PhotonBlue.Tests;

public class GameFileIndexerTests
{
    [Fact]
    public void Should_List_Files()
    {
        var index = new GameFileIndexer();
        index.LoadFromDataPath(@"D:\PHANTASYSTARONLINE2_JP\pso2_bin\data");
        Assert.All(index.ListFiles().Take(5000), path => Assert.True(path.FileName.Length > 0));
    }
}