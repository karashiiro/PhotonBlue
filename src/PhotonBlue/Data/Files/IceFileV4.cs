using System.Diagnostics;
using PhotonBlue.Extensions;
using PhotonBlue.Ooz;

namespace PhotonBlue.Data.Files;

public class IceFileV4 : IceFile
{
    public struct GroupHeader
    {
        public uint RawSize;
        public uint CompressedSize;
        public uint FileCount;
        public uint CRC32;

        public static GroupHeader Read(BinaryReader reader)
        {
            return new()
            {
                RawSize = reader.ReadUInt32(),
                CompressedSize = reader.ReadUInt32(),
                FileCount = reader.ReadUInt32(),
                CRC32 = reader.ReadUInt32(),
            };
        }
    }

    public GroupHeader Group1 { get; private set; }
    public GroupHeader Group2 { get; private set; }

    public IList<FileEntry> Group1Entries { get; private set; }
    public IList<FileEntry> Group2Entries { get; private set; }

    public IceFileV4(Stream data) : base(data)
    {
        Group1Entries = Array.Empty<FileEntry>();
        Group2Entries = Array.Empty<FileEntry>();
    }

    public override void LoadFile()
    {
        // Read the ICE archive header
        base.LoadFile();

        // This is only applicable for unencrypted files
        Group1 = GroupHeader.Read(Reader);
        Group2 = GroupHeader.Read(Reader);
        Reader.Seek(0x10, SeekOrigin.Current);

        var group1Data = new byte[Group1.RawSize];
        var group2Data = new byte[Group2.RawSize];

        var group1DataCompressed = Reader.ReadBytes(Convert.ToInt32(Group1.CompressedSize));
        var group2DataCompressed = Reader.ReadBytes(Convert.ToInt32(Group2.CompressedSize));

        // Decompress the archive contents
        if (Header.Flags.HasFlag(IceFileFlags.Kraken))
        {
            Kraken.Decompress(group1DataCompressed, Group1.CompressedSize, group1Data, Group1.RawSize);
            Kraken.Decompress(group2DataCompressed, Group2.CompressedSize, group2Data, Group2.RawSize);
        }
        else
        {
            using var group1Stream = new MemoryStream(group1DataCompressed);
            using var group1IceStream = new IcePrsInputStream(group1Stream);
            using var group1DecompressionStream = new PrsStream(group1IceStream);
            var group1Read = group1DecompressionStream.Read(group1Data, 0, group1Data.Length);
            Debug.Assert(group1Read == group1Data.Length);

            using var group2Stream = new MemoryStream(group2DataCompressed);
            using var group2IceStream = new IcePrsInputStream(group2Stream);
            using var group2DecompressionStream = new PrsStream(group2IceStream);
            var group2Read = group2DecompressionStream.Read(group2Data, 0, group2Data.Length);
            Debug.Assert(group2Read == group2Data.Length);
        }

        Group1Entries = UnpackGroup(Group1, group1Data);
        Group2Entries = UnpackGroup(Group2, group2Data);
    }

    private static FileEntry[] UnpackGroup(GroupHeader header, byte[] data)
    {
        using var buf = new MemoryStream(data);
        using var br = new BinaryReader(buf);
        return Enumerable.Repeat(br, Convert.ToInt32(header.FileCount))
            .Select<BinaryReader, FileEntry?>(reader =>
            {
                var basePos = reader.BaseStream.Position;
                var entryHeader = FileEntryHeader.Read(reader);
                reader.Seek(basePos + entryHeader.HeaderSize, SeekOrigin.Begin);
                var entryData = reader.ReadBytes(entryHeader.DataSize);
                return new FileEntry(entryHeader, entryData);
            })
            .Where(e => e.HasValue)
            .Select(e => e!.Value)
            .ToArray();
    }
}