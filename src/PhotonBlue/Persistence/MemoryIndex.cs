using System.Collections.Concurrent;

namespace PhotonBlue.Persistence;

public class MemoryIndex : IGameFileIndex
{
    private readonly ConcurrentDictionary<string, IndexRepository> _repositories = new();
    private readonly ConcurrentDictionary<string, IndexPack> _packs = new();
    private readonly ConcurrentDictionary<string, IndexFileEntry> _fileEntries = new();

    public IEnumerable<IndexRepository> GetAllRepositories()
    {
        return _repositories.Values;
    }

    public IEnumerable<IndexPack> GetAllPacks()
    {
        return _packs.Values;
    }

    public IEnumerable<IndexPack> GetAllPacks(DateTime updatedBefore)
    {
        return GetAllPacks().Where(pack => pack.UpdatedAt < updatedBefore);
    }

    public IEnumerable<IndexFileEntry> GetAllFileEntries()
    {
        return _fileEntries.Values;
    }

    public void StoreRepository(IndexRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository.Name);
        _repositories[repository.Name] = repository;
    }

    public void StorePack(IndexPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack.Hash);
        _packs[pack.Hash] = pack;
    }

    public void StoreFileEntry(IndexFileEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry.FileName);
        _fileEntries[entry.FileName] = entry;
    }

    public void UpdateRepository(IndexRepository repository)
    {
        StoreRepository(repository);
    }

    public void UpdatePack(IndexPack pack)
    {
        StorePack(pack);
    }

    public void UpdateFileEntry(IndexFileEntry entry)
    {
        StoreFileEntry(entry);
    }

    public void DeleteRepository(IndexRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository.Name);
        _repositories.TryRemove(repository.Name, out _);
    }

    public void DeletePack(IndexPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack.Hash);
        _repositories.TryRemove(pack.Hash, out _);
    }

    public void DeleteFileEntry(IndexFileEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry.FileName);
        _repositories.TryRemove(entry.FileName, out _);
    }
}