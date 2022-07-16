// ReSharper disable NotAccessedField.Global

using System.Text;

namespace PhotonBlue.Data.Files;

public abstract class IceFile : FileResource
{
    [Flags]
    public enum IceFileFlags : uint
    {
        Encrypted = 0x00000001,
        Kraken = 0x00000008,
    }

    public struct FileHeader
    {
        public uint Magic; // ICE
        public uint Reserved1;
        public uint Version;
        public uint Reserved2;
        public uint Reserved3;
        public uint CRC32;
        public IceFileFlags Flags;
        public int FileSize;
        public byte[] BlowfishMagic; // 0x100 bytes

        public static FileHeader Read(BinaryReader reader)
        {
            return new()
            {
                Magic = reader.ReadUInt32(),
                Reserved1 = reader.ReadUInt32(),
                Version = reader.ReadUInt32(),
                Reserved2 = reader.ReadUInt32(),
                Reserved3 = reader.ReadUInt32(),
                CRC32 = reader.ReadUInt32(),
                Flags = (IceFileFlags)reader.ReadUInt32(),
                FileSize = reader.ReadInt32(),
                BlowfishMagic = reader.ReadBytes(0x100),
            };
        }
    }

    public struct FileEntry
    {
        public FileEntryHeader Header;
        public byte[] Data;

        public FileEntry(FileEntryHeader header, byte[] data)
        {
            Header = header;
            Data = data;
        }
    }

    public struct FileEntryHeader
    {
        public uint Magic;
        public int FileSize;
        public int DataSize; // Size without this header
        public int HeaderSize;
        public int FileNameLength;
        public uint Reserved1;
        public uint Reserved2;
        public uint Reserved3;
        public byte[] Reserved4; // 0x20 bytes
        public byte[] FileNameRaw;

        public string FileName => Encoding.UTF8.GetString(FileNameRaw).TrimEnd('\u0000');

        public static FileEntryHeader Read(BinaryReader reader)
        {
            var header = new FileEntryHeader
            {
                Magic = reader.ReadUInt32(),
                FileSize = reader.ReadInt32(),
                DataSize = reader.ReadInt32(),
                HeaderSize = reader.ReadInt32(),
                FileNameLength = reader.ReadInt32(),
                Reserved1 = reader.ReadUInt32(),
                Reserved2 = reader.ReadUInt32(),
                Reserved3 = reader.ReadUInt32(),
                Reserved4 = reader.ReadBytes(0x20),
            };

            // This should always end up being either 0x10 or 0x20 bytes
            header.FileNameRaw = reader.ReadBytes(header.FileNameLength);

            return header;
        }
    }

    public FileHeader Header { get; private set; }

    protected IceFile(Stream data) : base(data)
    {
    }

    public override void LoadFile()
    {
        Header = FileHeader.Read(Reader);
    }
}