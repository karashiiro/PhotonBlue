using System.Collections.Concurrent;

namespace PhotonBlue.Persistence;

public class MemoryIndex : IGameFileIndex
{
    private readonly ConcurrentDictionary<string, IndexRepository> _repositories = new();
    private readonly ConcurrentDictionary<string, IndexPack> _packs = new();
    private readonly ConcurrentDictionary<(string, string), IndexFileEntry> _fileEntries = new();

    public IEnumerable<IndexRepository> GetAllRepositories()
    {
        return _repositories.Values;
    }

    public IndexRepository? GetRepository(string name)
    {
        return _repositories.TryGetValue(name, out var repository) ? repository : null;
    }

    public IEnumerable<IndexPack> GetAllPacks()
    {
        return _packs.Values;
    }

    public IEnumerable<IndexPack> GetAllPacks(DateTime updatedBefore)
    {
        return GetAllPacks().Where(pack => pack.UpdatedAt != null && pack.UpdatedAt < updatedBefore);
    }

    public IndexPack? GetPack(string hash)
    {
        return _packs.TryGetValue(hash, out var pack) ? pack : null;
    }

    public IEnumerable<IndexFileEntry> GetAllFileEntries()
    {
        return _fileEntries.Values;
    }

    public IEnumerable<IndexFileEntry> GetAllFileEntries(string fileName)
    {
        return GetAllFileEntries().Where(entry => entry.FileName == fileName);
    }

    public IndexFileEntry? GetFileEntry(string packHash, string fileName)
    {
        return _fileEntries.TryGetValue((packHash, fileName), out var fileEntry) ? fileEntry : null;
    }

    public void StoreRepository(IndexRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository.Name);
        _repositories[repository.Name] = repository;
    }

    public void StorePack(IndexPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack.Hash);
        ArgumentNullException.ThrowIfNull(pack.Repository);

        if (GetRepository(pack.Repository) == null)
        {
            throw new ArgumentException("Pack repository does not exist.");
        }

        _packs[pack.Hash] = pack;
    }

    public void StoreFileEntry(IndexFileEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry.PackHash);
        ArgumentNullException.ThrowIfNull(entry.FileName);

        if (GetPack(entry.PackHash) == null)
        {
            throw new ArgumentException("File pack does not exist.");
        }

        _fileEntries[(entry.PackHash, entry.FileName)] = entry;
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