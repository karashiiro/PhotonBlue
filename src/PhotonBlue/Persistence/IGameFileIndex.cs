namespace PhotonBlue.Persistence;

public interface IGameFileIndex
{
    IEnumerable<IndexRepository> GetAllRepositories();

    IndexRepository? GetRepository(string name);

    IEnumerable<IndexPack> GetAllPacks();

    IndexPack? GetPack(string hash);

    IEnumerable<IndexFileEntry> GetAllFileEntries();

    IEnumerable<IndexFileEntry> GetAllFileEntries(string fileName);

    IndexFileEntry? GetFileEntry(string packHash, string fileName);

    void StoreRepository(IndexRepository repository);

    void StorePack(IndexPack pack);

    void StoreFileEntry(IndexFileEntry entry);

    void UpdateRepository(IndexRepository repository);

    void UpdatePack(IndexPack pack);

    void UpdateFileEntry(IndexFileEntry entry);

    void DeleteRepository(IndexRepository repository);

    void DeletePack(IndexPack pack);

    void DeleteFileEntry(IndexFileEntry entry);
}