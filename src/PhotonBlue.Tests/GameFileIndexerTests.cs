using System.Linq;
using Xunit;

namespace PhotonBlue.Tests;

public class GameFileIndexerTests
{
    [Fact]
    public void Should_List_Files()
    {
        var gameData = new GameData(@"D:\PHANTASYSTARONLINE2_JP\pso2_bin");
        gameData.Index.LoadFromDataPath(@"D:\PHANTASYSTARONLINE2_JP\pso2_bin");
        Assert.All(gameData.Index.ListFiles().Take(40000), path => Assert.True(path.FileName!.Length > 0));
    }
}