using PhotonBlue;

IGameFileIndexer index = new GameFileIndexer();
index.LoadFromDataPath(@"D:\PHANTASYSTARONLINE2_JP\pso2_bin\data");
foreach (var parsedFilePath in index.ListFiles())
{
    Console.WriteLine(parsedFilePath);
}