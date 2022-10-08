// ReSharper disable NotAccessedField.Global

using System.Diagnostics;
using System.Text;
using PhotonBlue.Cryptography;
using PhotonBlue.Extensions;

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
        public uint Const80;
        public uint ConstFF;
        public uint CRC32;
        public IceFileFlags Flags;
        public uint FileSize;
        public byte[] BlowfishMagic; // 0x100 bytes, empty if not encrypted

        public static FileHeader Read(BinaryReader reader)
        {
            var header = new FileHeader
            {
                Magic = reader.ReadUInt32(),
                Reserved1 = reader.ReadUInt32(),
                Version = reader.ReadUInt32(),
                Const80 = reader.ReadUInt32(),
                ConstFF = reader.ReadUInt32(),
                CRC32 = reader.ReadUInt32(),
                Flags = (IceFileFlags)reader.ReadUInt32(),
                FileSize = reader.ReadUInt32(),
                BlowfishMagic = reader.ReadBytes(0x100),
            };

            Debug.Assert(header.Magic == 0x454349, "Bad magic detected!");
            Debug.Assert(header.Const80 == 0x80);
            Debug.Assert(header.ConstFF == 0xFF);

            return header;
        }
    }

    public class FileEntry
    {
        public FileEntryHeader Header { get; }
        public byte[] Data { get; }
        
        public FileEntry(FileEntryHeader header)
        {
            Header = header;
            Data = Array.Empty<byte>();
        }

        public FileEntry(FileEntryHeader header, byte[] data)
        {
            Header = header;
            Data = data;
            
            Debug.Assert(header.DataSize == data.Length, "File size mismatch.");
        }
    }

    public struct FileEntryHeader
    {
        public uint Magic;
        public uint EntrySize;
        public uint DataSize; // Size without this header
        public uint HeaderSize;
        public uint FileNameLength;
        public uint Reserved1;
        public uint Reserved2;
        public uint Reserved3;
        public byte[] Reserved4; // 0x20 bytes
        public byte[] FileNameRaw;

        public string FileName => new(Encoding.UTF8.GetString(FileNameRaw).AsSpan().TrimEnd('\u0000'));

        public static FileEntryHeader Read(BinaryReader reader)
        {
            var header = new FileEntryHeader
            {
                Magic = reader.ReadUInt32(),
                EntrySize = reader.ReadUInt32(),
                DataSize = reader.ReadUInt32(),
                HeaderSize = reader.ReadUInt32(),
                FileNameLength = reader.ReadUInt32(),
                Reserved1 = reader.ReadUInt32(),
                Reserved2 = reader.ReadUInt32(),
                Reserved3 = reader.ReadUInt32(),
                Reserved4 = reader.ReadBytes(0x20),
            };

            Debug.Assert(header.HeaderSize % 16 == 0, "Invalid header size detected.");
            Debug.Assert(header.EntrySize > 0x40, "File size mismatch detected.");
            Debug.Assert(header.HeaderSize > 0x40, "Header size mismatch detected.");
            Debug.Assert(header.FileNameLength < header.HeaderSize, "Unexpected file name length detected.");

            header.FileNameRaw = reader.ReadBytes(Convert.ToInt32(header.FileNameLength));

            // The name area size is a multiple of 0x10, but the name length can be less than that.
            // We need to seek to the end of the region to read the next block correctly.
            reader.Seek(0x10 - header.FileNameLength % 0x10, SeekOrigin.Current);

            return header;
        }
    }

    public FileHeader Header { get; private set; }
    
    protected IceFile()
    {
    }

    protected IceFile(Stream data, IObjectPool<BlowfishGpuHandle, Blowfish> blowfishGpuPool) : base(data, blowfishGpuPool)
    {
    }

    public override void LoadFile()
    {
        Debug.Assert(Reader != null);
        Header = FileHeader.Read(Reader);
    }
    
    public override void LoadHeadersOnly()
    {
        Debug.Assert(Reader != null);
        Header = FileHeader.Read(Reader);
    }
}