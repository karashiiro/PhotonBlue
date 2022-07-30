using PhotonBlue.Data;

namespace PhotonBlue;

public interface IGameFileIndexer
{
    public void LoadFromDataPath(string dataPath);

    public IEnumerable<ParsedFilePath> ListFiles();

    public int PacksRead { get; }

    public int PackCount { get; }
}