using System.Linq;
using Xunit;

namespace PhotonBlue.Tests;

public class GameFileIndexerTests
{
    [Fact]
    public void GameFileIndexer_Lists_Files()
    {
        var index = new GameFileIndexer();
        index.LoadFromDataPath(@"D:\PHANTASYSTARONLINE2_JP\pso2_bin\data");
        Assert.All(index.ListFiles().Take(100), path => Assert.True(path.FileName.Length > 0));
    }
}