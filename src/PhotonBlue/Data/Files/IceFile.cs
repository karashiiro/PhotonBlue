// ReSharper disable NotAccessedField.Global

using System.Diagnostics;
using System.Text;
using PhotonBlue.Extensions;

namespace PhotonBlue.Data.Files;

public class IceFile : FileResource
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
                FileSize = reader.ReadUInt32(),
                BlowfishMagic = reader.ReadBytes(0x100),
            };
        }
    }

    public struct FileEntry
    {
        public FileEntryHeader Header;
        // ReSharper disable once FieldCanBeMadeReadOnly.Global
        public byte[] Data;

        public FileEntry(FileEntryHeader header, byte[] data)
        {
            Header = header;
            Data = data;
            
            Debug.Assert(Header.DataSize == Data.Length, "File data size mismatch.");
        }
    }

    public struct FileEntryHeader
    {
        public uint Magic;
        public uint FileSize;
        public uint DataSize; // Size without this header
        public uint HeaderSize;
        public uint FileNameLength;
        public uint Reserved1;
        public uint Reserved2;
        public uint Reserved3;
        public byte[] Reserved4; // 0x20 bytes
        public byte[] FileNameRaw;

        public string FileName => Encoding.UTF8.GetString(FileNameRaw);

        public static FileEntryHeader Read(BinaryReader reader)
        {
            var header = new FileEntryHeader
            {
                Magic = reader.ReadUInt32(),
                FileSize = reader.ReadUInt32(),
                DataSize = reader.ReadUInt32(),
                HeaderSize = reader.ReadUInt32(),
                FileNameLength = reader.ReadUInt32(),
                Reserved1 = reader.ReadUInt32(),
                Reserved2 = reader.ReadUInt32(),
                Reserved3 = reader.ReadUInt32(),
                Reserved4 = reader.ReadBytes(0x20),
            };

            // This should always end up being less than 0x20 bytes
            header.FileNameRaw = reader.ReadBytes(Convert.ToInt32(header.FileNameLength));
            
            // The name area size is a multiple of 0x10, but the name length can be less than that.
            // We need to seek to the end of the region to read the next block correctly.
            reader.Seek(0x10 - header.FileNameLength % 0x10, SeekOrigin.Current);

            Debug.Assert(header.FileNameLength <= 0x20, "Unexpected file name length detected.");

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