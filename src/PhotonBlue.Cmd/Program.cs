using PhotonBlue;

using var gameData = new GameData(@"D:\PHANTASYSTARONLINE2_JP\pso2_bin");
gameData.Index.LoadFromDataPath(@"D:\PHANTASYSTARONLINE2_JP\pso2_bin\data");
foreach (var parsedFilePath in gameData.Index.ListFiles())
{
    Console.WriteLine(parsedFilePath);
}