using System.Reflection;
using System.Text;
using PhotonBlue.Attributes;

namespace PhotonBlue.Data;

public class FileHandle<T> : BaseFileHandle where T : FileResource, new()
{
    internal FileHandle(string path) : base(path)
    {
    }

    public override void Load()
    {
        State = FileState.Loading;

        try
        {
            var file = new T();
            using var data = File.OpenRead(Path);
            file.BaseStream = data;
            file.Reader = new BinaryReader(data);

            var magicAttr = typeof(T).GetCustomAttribute<MagicAttribute>();
            if (magicAttr != null)
            {
                var magic = file.Reader.ReadBytes(4);
                var magicStr = Encoding.UTF8.GetString(magic).TrimEnd('\u0000');
                if (magicStr == magicAttr.Magic)
                {
                    file.BaseStream.Seek(0, SeekOrigin.Begin);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Invalid file magic: Expected {magicAttr.Magic}, got {magicStr}.");
                }
            }

            file.LoadFile();

            State = FileState.Loaded;
            Instance = file;
        }
        catch
        {
            State = FileState.Error;
        }
    }

    public override void LoadHeadersOnly()
    {
        State = FileState.Loading;

        try
        {
            var file = new T();
            using var data = File.OpenRead(Path);
            file.BaseStream = data;
            file.Reader = new BinaryReader(data);

            var magicAttr = typeof(T).GetCustomAttribute<MagicAttribute>();
            if (magicAttr != null)
            {
                var magic = file.Reader.ReadBytes(4);
                var magicStr = Encoding.UTF8.GetString(magic).TrimEnd('\u0000');
                if (magicStr == magicAttr.Magic)
                {
                    file.BaseStream.Seek(0, SeekOrigin.Begin);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Invalid file magic: Expected {magicAttr.Magic}, got {magicStr}.");
                }
            }

            file.LoadHeadersOnly();

            State = FileState.Loaded;
            Instance = file;
        }
        catch
        {
            State = FileState.Error;
        }
    }

    /// <summary>
    /// Returns the <see cref="FileResource"/> or null if it isn't loaded or failed to load.
    /// </summary>
    public T? Value
    {
        get
        {
            if (HasValue)
            {
                return (T?)Instance;
            }

            return null;
        }
    }
}