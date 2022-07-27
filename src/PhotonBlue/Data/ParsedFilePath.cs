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
        
        var pathParts = path.Split('/');
        var fileName = pathParts[^1];
        var packName = pathParts[^2];
        var resourceFolder = string.Join('/', pathParts.Take(pathParts.Length - 2));
        return new ParsedFilePath
        {
            ResourceFolder = resourceFolder,
            PackName = packName,
            FileName = fileName,
            RawPath = path,
        };
    }
    
    public static implicit operator string(ParsedFilePath o) => o.RawPath!;
}