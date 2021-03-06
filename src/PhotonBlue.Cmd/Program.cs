using PhotonBlue;

const string binPath = @"D:\PHANTASYSTARONLINE2_JP\pso2_bin";
using var gameData = new GameData(binPath);
gameData.Indexer.LoadFromDataPath(binPath);

foreach (var path in gameData.Indexer.ListFiles())
{
    Console.WriteLine($"{gameData.Indexer.PacksRead}/{gameData.Indexer.PackCount}: {path.FileName}");
}