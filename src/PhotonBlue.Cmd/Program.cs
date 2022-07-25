using PhotonBlue;

using var gameData = new GameData(@"D:\PHANTASYSTARONLINE2_JP\pso2_bin");
gameData.Index.LoadFromDataPath(@"D:\PHANTASYSTARONLINE2_JP\pso2_bin");

var i = 0;
foreach (var path in gameData.Index.ListFiles())
{
    i++;
    Console.WriteLine($"{i}/{gameData.Index.Count}: {path.FileName}");
}