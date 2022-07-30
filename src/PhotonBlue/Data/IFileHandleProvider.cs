namespace PhotonBlue.Data;

public interface IFileHandleProvider
{
    /// <summary>
    /// Creates a new handle to a game file but does not load it. The file will become loaded at an
    /// indeterminate later time. The implementation of this interface must either provide a way to
    /// trigger file loads, or simply manage that functionality itself.
    /// </summary>
    /// <param name="path">The path to the file to load.</param>
    /// <param name="loadComplete">Whether or not to load the complete file when processing the load operation.</param>
    /// <typeparam name="T">The type of <see cref="FileResource"/> to load.</typeparam>
    /// <returns>A handle to the file to be loaded.</returns>
    FileHandle<T> CreateHandle<T>(string path, bool loadComplete = true) where T : FileResource, new();
}