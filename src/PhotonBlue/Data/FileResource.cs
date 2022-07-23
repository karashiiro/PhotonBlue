using System.Reflection;
using System.Text;
using PhotonBlue.Attributes;

namespace PhotonBlue.Data;

public abstract class FileResource
{
    internal Stream? BaseStream { get; set; }
    internal BinaryReader? Reader { get; set; }

    protected FileResource()
    {
    }

    protected FileResource(Stream data)
    {
        BaseStream = data;
        Reader = new BinaryReader(data);
    }

    public abstract void LoadFile();

    /// <summary>
    /// Loads the file's headers, without loading any nonessential data.
    /// This may not be meaningfully implemented for some files.
    /// </summary>
    /// <exception cref="NotSupportedException"></exception>
    public virtual void LoadHeadersOnly()
    {
        throw new NotSupportedException();
    }

    public static T FromStream<T>(Stream data) where T : FileResource, new()
    {
        var file = new T
        {
            BaseStream = data,
            Reader = new BinaryReader(data),
        };

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

        return file;
    }
}