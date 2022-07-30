namespace PhotonBlue.Persistence;

public interface IGameFileIndex
{
    IEnumerable<IndexRepository> GetAllRepositories();

    IEnumerable<IndexPack> GetAllPacks();

    IEnumerable<IndexPack> GetAllPacks(DateTime updatedBefore);

    IEnumerable<IndexFileEntry> GetAllFileEntries();

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