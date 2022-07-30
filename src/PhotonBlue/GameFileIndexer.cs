using PhotonBlue.Data;
using PhotonBlue.Data.Files;
using PhotonBlue.Persistence;

namespace PhotonBlue;

public class GameFileIndexer : IGameFileIndexer
{
    private readonly List<ParsedFilePath> _files;
    private readonly IFileHandleProvider _fileHandleProvider;
    private readonly IGameFileIndex _index;

    public int PacksRead { get; private set; }
    
    public int PackCount => _files.Count;

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
        var allFiles = Enumerable.Empty<(string, string, string?, string)>();

        if (Directory.Exists(win32Path))
        {
            allFiles = allFiles.Concat(Directory.EnumerateFiles(win32Path, "", SearchOption.AllDirectories)
                .Select<string, (string, string, string?, string)>(file =>
                    (file, "win32", null, Path.GetFileName(file))));
        }

        if (Directory.Exists(win32RebootPath))
        {
            allFiles = allFiles.Concat(Directory.EnumerateFiles(win32RebootPath, "", SearchOption.AllDirectories)
                .Select<string, (string, string, string?, string)>(file => (file, "win32reboot",
                    new DirectoryInfo(Path.GetDirectoryName(file)!).Name, Path.GetFileName(file))));
        }

        // Process the files
        _files.AddRange(allFiles
            .Select(file =>
            {
                var (p, s, r, f) = file;

                // Currently only processing ICE files; will add more once this works
                var handle = _fileHandleProvider.CreateHandle<IceV4File>(p, false);
                return ((BaseFileHandle)handle, s, r, f);
            })
            .SelectMany(file =>
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
                    return ice.Group1Entries
                        .Select(entry =>
                            ParsedFilePath.ParseFilePath(
                                $"{s}/{(r != null ? r + '/' : "")}{f}/{entry.Header.FileName}"))
                        .Concat(ice.Group2Entries
                            .Select(entry =>
                                ParsedFilePath.ParseFilePath(
                                    $"{s}/{(r != null ? r + '/' : "")}{f}/{entry.Header.FileName}")));
                }

                PacksRead++;
                return Enumerable.Empty<ParsedFilePath?>();
            })
            .Where(path => path is not null)
            .Select(path => path!));
    }

    public IEnumerable<ParsedFilePath> ListFiles()
    {
        return _files;
    }
}