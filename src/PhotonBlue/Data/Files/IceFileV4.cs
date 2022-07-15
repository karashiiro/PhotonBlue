﻿using PhotonBlue.Extensions;
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

    public IList<FileEntry>? Group1Entries { get; private set; }
    public IList<FileEntry>? Group2Entries { get; private set; }

    public IceFileV4(Stream data) : base(data)
    {
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
        Kraken.Decompress(Reader.ReadBytes(Convert.ToInt32(Group1.CompressedSize)), Group1.CompressedSize, group1Data,
            Group1.RawSize);

        var group2Data = new byte[Group2.RawSize];
        Kraken.Decompress(Reader.ReadBytes(Convert.ToInt32(Group2.CompressedSize)), Group2.CompressedSize, group2Data,
            Group2.RawSize);

        Group1Entries = UnpackGroup(Group1, group1Data);
        Group2Entries = UnpackGroup(Group2, group2Data);
    }

    private static FileEntry[] UnpackGroup(GroupHeader header, byte[] data)
    {
        using var buf = new MemoryStream(data);
        using var reader = new BinaryReader(buf);
        return Enumerable.Range(0, Convert.ToInt32(header.FileCount))
            .Select<int, FileEntry?>(_ =>
            {
                try
                {
                    var entryHeader = FileEntryHeader.Read(reader);
                    var entryData = reader.ReadBytes(entryHeader.DataSize);
                    return new FileEntry
                    {
                        Header = entryHeader,
                        Data = entryData,
                    };
                }
                catch (EndOfStreamException)
                {
                    // No more data is available, for some reason
                    return null;
                }
            })
            .Where(e => e.HasValue)
            .Select(e => e!.Value)
            .ToArray();
    }
}