using PhotonBlue.Data;

namespace PhotonBlue;

public interface IGameFileIndexer
{
    public void LoadFromDataPath(string dataPath);
    
    public IEnumerable<ParsedFilePath> ListFiles();
}