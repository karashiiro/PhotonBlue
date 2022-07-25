using PhotonBlue.Data;
using PhotonBlue.Data.Files;

namespace PhotonBlue;

public class GameFileIndexer : IGameFileIndexer
{
    private readonly FileHandleManager _fileHandleManager;
    private readonly List<(BaseFileHandle, string, string?, string)> _files;

    public int Count => _files.Count;

    public GameFileIndexer(FileHandleManager fileHandleManager)
    {
        _fileHandleManager = fileHandleManager;
        _files = new List<(BaseFileHandle, string, string?, string)>();
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
            allFiles = allFiles.Concat(Directory.EnumerateFiles(win32Path, "", SearchOption.TopDirectoryOnly)
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
                var handle = _fileHandleManager.CreateHandle<IceV4File>(p, false);
                return ((BaseFileHandle)handle, s, r, f);
            }));
    }

    public IEnumerable<ParsedFilePath> ListFiles()
    {
        return _files
            .SelectMany(file =>
            {
                var (h, s, r, f) = file;

                // Currently only processing ICE files; will add more once this works
                if (h is FileHandle<IceV4File> handle)
                {
                    while (h.State != BaseFileHandle.FileState.Loaded)
                    {
                        if (h.State == BaseFileHandle.FileState.Error)
                        {
                            return Enumerable.Empty<ParsedFilePath?>();
                        }

                        Thread.Yield();
                    }

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

                return Enumerable.Empty<ParsedFilePath?>();
            })
            .Where(path => path is not null)
            .Select(path => path!);
    }
}