namespace PhotonBlue.Data;

public class ParsedFilePath
{
    /// <summary>
    /// The file's resource folder; win32 or win32reboot/{byte}.
    /// </summary>
    public string? ResourceFolder { get; private init; }

    /// <summary>
    /// The final part of the file path on the filesystem. Multiple
    /// logical files can exist in a single packed file.
    /// </summary>
    public string? PackName { get; private init; }

    /// <summary>
    /// The file's extracted name.
    /// </summary>
    public string? FileName { get; private init; }

    /// <summary>
    /// The raw path provided when parsing the initial path.
    /// </summary>
    public string? RawPath { get; private init; }

    public static ParsedFilePath? ParseFilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        ReadOnlySpan<char> pathSpan = path;
        var pathSpanIndex = pathSpan.LastIndexOf('/');
        var pathPart = pathSpan[..pathSpanIndex];
        var pathPartIndex = pathPart.LastIndexOf('/');

        var fileName = pathSpan[(pathSpanIndex + 1)..];
        var packName = pathPart[(pathPartIndex + 1)..];
        var resourceFolder = pathSpan[..pathPartIndex];
        return new ParsedFilePath
        {
            ResourceFolder = new string(resourceFolder),
            PackName = new string(packName),
            FileName = new string(fileName),
            RawPath = path,
        };
    }

    public static implicit operator string(ParsedFilePath o) => o.RawPath!;
}