using PhotonBlue;

const string binPath = @"D:\PHANTASYSTARONLINE2_JP\pso2_bin";
using var gameData = new GameData(binPath);
gameData.Index.LoadFromDataPath(binPath);

foreach (var path in gameData.Index.ListFiles())
{
    Console.WriteLine($"{gameData.Index.DiskFilesRead}/{gameData.Index.DiskFileCount}: {path.FileName}");
}