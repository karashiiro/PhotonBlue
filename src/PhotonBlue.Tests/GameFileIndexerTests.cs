using Xunit;

namespace PhotonBlue.Tests;

public class GameFileIndexerTests
{
    [Fact]
    public void Should_List_Files()
    {
        using var gameData = new GameData(@"D:\PHANTASYSTARONLINE2_JP\pso2_bin");
        gameData.Indexer.LoadFromDataPath(@"D:\PHANTASYSTARONLINE2_JP\pso2_bin");
        Assert.All(gameData.Indexer.ListFiles(), path => Assert.True(path.FileName!.Length > 0));
    }
}