// ReSharper disable NotAccessedField.Global

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
        public uint FileSize;
        public byte[] Reserved4; // 0x100 bytes

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
                FileSize = reader.ReadUInt32(),
                Reserved4 = reader.ReadBytes(0x100),
            };
        }
    }

    public FileHeader Header { get; private set; }

    public IceFile(Stream data) : base(data)
    {
    }

    public override void LoadFile()
    {
        Header = FileHeader.Read(Reader);
    }
}