using PhotonBlue;

var gameData = new GameData(@"D:\PHANTASYSTARONLINE2_JP\pso2_bin");
gameData.Index.LoadFromDataPath(@"D:\PHANTASYSTARONLINE2_JP\pso2_bin");

foreach (var path in gameData.Index.ListFiles())
{
    Console.WriteLine($"{gameData.Index.DiskFilesRead}/{gameData.Index.DiskFileCount}: {path.FileName}");
}