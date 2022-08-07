using System.Diagnostics;
using PhotonBlue.Data;
using PhotonBlue.Data.Files;
using PhotonBlue.Persistence;

namespace PhotonBlue;

public class GameFileIndexer : IGameFileIndexer
{
    private readonly IFileHandleProvider _fileHandleProvider;
    private readonly IGameFileIndex _index;

    private IEnumerable<ParsedFilePath> _files;

    public int PacksRead { get; private set; }

    public int PackCount { get; private set; }

    public GameFileIndexer(IFileHandleProvider fileHandleProvider, IGameFileIndex index)
    {
        _files = new List<ParsedFilePath>();
        _fileHandleProvider = fileHandleProvider;
        _index = index;
    }

    public void LoadFromDataPath(string dataPath)
    {
        if (!Directory.Exists(dataPath))
        {
            throw new DirectoryNotFoundException(nameof(dataPath));
        }

        // Require at least one of the two resource folders to be present
        var subdirectories = Directory.GetDirectories(Path.Join(dataPath, "data"))
            .Select(path => new DirectoryInfo(path).Name)
            .ToArray();
        if (!subdirectories.Contains("win32") && !subdirectories.Contains("win32reboot"))
        {
            throw new ArgumentException(
                "Invalid game data path provided; expected pso2_bin!",
                nameof(dataPath));
        }

        // Enumerate all of the relevant file paths
        var win32Path = Path.Combine(dataPath, "data", "win32");
        var win32RebootPath = Path.Combine(dataPath, "data", "win32reboot");
        var allFiles = new List<(string, string, string?, string)>();

        if (Directory.Exists(win32Path))
        {
            allFiles.AddRange(Directory.EnumerateFiles(win32Path, "", SearchOption.AllDirectories)
                .Select<string, (string, string, string?, string)>(file =>
                    (file, "win32", null, Path.GetFileName(file))));
            if (_index.GetRepository("win32") == null)
            {
                _index.StoreRepository(new IndexRepository { Name = "win32" });
            }
        }

        if (Directory.Exists(win32RebootPath))
        {
            allFiles.AddRange(Directory.EnumerateFiles(win32RebootPath, "", SearchOption.AllDirectories)
                .Select<string, (string, string, string?, string)>(file => (file, "win32reboot",
                    new DirectoryInfo(Path.GetDirectoryName(file)!).Name, Path.GetFileName(file))));
            if (_index.GetRepository("win32reboot") == null)
            {
                _index.StoreRepository(new IndexRepository { Name = "win32reboot" });
            }
        }

        PackCount = allFiles.Count;

        // Dispatch jobs to the thread pool
        var jobs = allFiles
            .Select(file =>
            {
                var (p, s, r, f) = file;

                var hash = f;
                if (!string.IsNullOrEmpty(r))
                {
                    hash = r + f;
                }

                if (_index.GetPack(hash) == null)
                {
                    Debug.Assert(!hash.Contains('/'));
                    _index.StorePack(new IndexPack
                    {
                        Hash = hash,
                        Repository = s,
                        UpdatedAt = DateTime.UtcNow,
                    });
                }

                // Currently only processing ICE files; will add more once this works
                var handle = _fileHandleProvider.CreateHandle<IceV4File>(p, false);
                return ((BaseFileHandle)handle, s, r, f);
            })
            .ToList();

        // Create the enumerable that will produce results on-demand later
        _files = jobs.SelectMany(file =>
            {
                var (h, s, r, f) = file;

                // Currently only processing ICE files; will add more once this works
                if (h is FileHandle<IceV4File> handle)
                {
                    h.Reset.Wait();

                    if (h.State == BaseFileHandle.FileState.Error)
                    {
                        PacksRead++;
                        return Enumerable.Empty<ParsedFilePath?>();
                    }

                    PacksRead++;
                    var ice = handle.Value!;
                    var fileEntries = ice.Group1Entries
                        .Select(entry =>
                            ParsedFilePath.ParseFilePath(
                                $"{s}/{(r != null ? r + '/' : "")}{f}/{entry.Header.FileName}"))
                        .Concat(ice.Group2Entries
                            .Select(entry =>
                                ParsedFilePath.ParseFilePath(
                                    $"{s}/{(r != null ? r + '/' : "")}{f}/{entry.Header.FileName}")))
                        .Where(path => path != null)
                        .ToList();

                    if (!fileEntries.Any())
                    {
                        return Enumerable.Empty<ParsedFilePath?>();
                    }

                    var first = fileEntries[0];
                    Debug.Assert(first != null);
                    Debug.Assert(first.PackName != null);

                    var pack = _index.GetPack(first.PackName);
                    Debug.Assert(pack != null);

                    foreach (var fileEntry in fileEntries)
                    {
                        Debug.Assert(fileEntry != null);
                        Debug.Assert(fileEntry.PackName != null);
                        Debug.Assert(fileEntry.FileName != null);

                        var existing = _index.GetFileEntry(fileEntry.PackName, fileEntry.FileName);
                        if (existing != null)
                        {
                            _index.DeleteFileEntry(existing);
                        }

                        _index.StoreFileEntry(new IndexFileEntry
                        {
                            PackHash = fileEntry.PackName,
                            FileName = fileEntry.FileName,
                        });
                    }

                    return fileEntries;
                }

                PacksRead++;
                return Enumerable.Empty<ParsedFilePath?>();
            })
            .Where(path => path is not null)
            .Select(path => path!);
    }

    public IEnumerable<ParsedFilePath> ListFiles()
    {
        return _files;
    }
}