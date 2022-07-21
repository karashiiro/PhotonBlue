using System.Diagnostics;
using PhotonBlue.Cryptography;
using PhotonBlue.Extensions;
using PhotonBlue.Ooz;

namespace PhotonBlue.Data.Files;

public class IceV4File : IceFile
{
    public struct GroupHeader
    {
        public uint RawSize;
        public uint CompressedSize;
        public uint FileCount;
        public uint CRC32;

        public uint GetStoredSize()
        {
            return CompressedSize > 0 ? CompressedSize : RawSize;
        }

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

    private BlowfishKeys _keys;

    public IceV4File(Stream data) : base(data)
    {
        _keys = new BlowfishKeys();
        
        Group1Entries = Array.Empty<FileEntry>();
        Group2Entries = Array.Empty<FileEntry>();
    }

    public override void LoadFile()
    {
        LoadHeaders();

        // Set up some partitions that keep reads in the different sections of the archive
        // isolated from each other.
        var partitions = new long[] { 336, Group1.GetStoredSize(), Group2.GetStoredSize() };
        var partitionedStream = new PartitionedStream(Reader.BaseStream, partitions);

        // Skip the headers, which have already been read
        Debug.Assert(partitionedStream.Position == 336, "Stream is at an unexpected position.");
        partitionedStream.NextPartition();

        // Extract the archive contents
        LoadFileEntries(partitionedStream);
    }

    public void LoadFileHeadersOnly()
    {
        LoadHeaders();

        // Set up some partitions that keep reads in the different sections of the archive
        // isolated from each other.
        var partitions = new long[] { 336, Group1.GetStoredSize(), Group2.GetStoredSize() };
        var partitionedStream = new PartitionedStream(Reader.BaseStream, partitions);

        // Skip the headers, which have already been read
        Debug.Assert(partitionedStream.Position == 336, "Stream is at an unexpected position.");
        partitionedStream.NextPartition();

        // Extract the archive entry headers
        LoadFileEntriesHeadersOnly(partitionedStream);
    }

    private void LoadHeaders()
    {
        // Read the ICE archive header
        base.LoadFile();

        Debug.Assert(Header.Version == 4, "Incorrect ICE version detected!");

        // Decrypt the file headers, if necessary
        _keys = new BlowfishKeys();
        if (Header.Flags.HasFlag(IceFileFlags.Encrypted))
        {
            GetBlowfishKeys(_keys, Header.BlowfishMagic, Convert.ToInt32(Header.FileSize));
            var headersRaw = Reader.ReadBytes(0x30);
            var blowfish = new Blowfish(BitConverter.GetBytes(_keys.GroupHeadersKey));
            blowfish.Decrypt(ref headersRaw);

            using var headersMem = new MemoryStream(headersRaw);
            using var subReader = new BinaryReader(headersMem);

            Group1 = GroupHeader.Read(subReader);
            Group2 = GroupHeader.Read(subReader);
        }
        else
        {
            Group1 = GroupHeader.Read(Reader);
            Group2 = GroupHeader.Read(Reader);
            Reader.Seek(0x10, SeekOrigin.Current);
        }
    }
    
    private void LoadFileEntriesHeadersOnly(PartitionedStream partitionedStream)
    {
        // Extract the first group
        var group1Stream = HandleGroupExtraction(Group1, _keys.GroupDataKeys[0], partitionedStream);
        Group1Entries = UnpackGroupHeadersOnly(Group1, group1Stream);

        // Move to the group 2 partition
        partitionedStream.NextPartition();

        // Extract the next group
        var group2Stream = HandleGroupExtraction(Group2, _keys.GroupDataKeys[1], partitionedStream);
        Group2Entries = UnpackGroupHeadersOnly(Group2, group2Stream);
    }

    private void LoadFileEntries(PartitionedStream partitionedStream)
    {
        // Extract the first group
        var group1Stream = HandleGroupExtraction(Group1, _keys.GroupDataKeys[0], partitionedStream);
        Group1Entries = UnpackGroup(Group1, group1Stream);

        // Move to the group 2 partition
        partitionedStream.NextPartition();

        // Extract the next group
        var group2Stream = HandleGroupExtraction(Group2, _keys.GroupDataKeys[1], partitionedStream);
        Group2Entries = UnpackGroup(Group2, group2Stream);
    }

    private Stream HandleGroupExtraction(GroupHeader group, IReadOnlyList<uint> keys, Stream data)
    {
        return data.Length == 0 ? data : DecodeGroup(group, keys, data);
    }

    private const int SecondPassThreshold = 102400;

    /// <summary>
    /// Decrypts and decompresses an ICE group.
    /// </summary>
    /// <param name="group">The group header.</param>
    /// <param name="keys">The group's decryption keys, if the file is encrypted.</param>
    /// <param name="data">The raw group data.</param>
    private Stream DecodeGroup(GroupHeader group, IReadOnlyList<uint> keys, Stream data)
    {
        var decryptStream = PrepareGroupDecryption(data, keys);
        // We use the input buffer as a scratch buffer for reading in the
        // decrypted data. This doesn't cause any decryption issues since
        // we're not reading the data more than once.
        return DecompressGroup(group, decryptStream);
    }

    /// <summary>
    /// Prepares a decryption stream over the provided encrypted group data stream.
    /// </summary>
    /// <param name="data">The group data stream.</param>
    /// <param name="keys">The group's decryption keys, if the file is encrypted.</param>
    /// <returns>A new stream that can decrypt data while reading it in.</returns>
    private Stream PrepareGroupDecryption(Stream data, IReadOnlyList<uint> keys)
    {
        var decryptStream = data;
        if (Header.Flags.HasFlag(IceFileFlags.Encrypted))
        {
            // These are all managed streams without disposable resources, so we
            // don't need to worry about disposing them properly.
            var floatageFish = new FloatageFishDecryptionStream(data, keys[0], 16);
            decryptStream = new BlowfishDecryptionStream(floatageFish, BitConverter.GetBytes(keys[0]));
            if (data.Length <= SecondPassThreshold)
            {
                decryptStream = new BlowfishDecryptionStream(decryptStream, BitConverter.GetBytes(keys[1]));
            }
        }

        return decryptStream;
    }

    /// <summary>
    /// Decompresses an ICE group.
    /// </summary>
    /// <param name="group">The group header.</param>
    /// <param name="inputStream">The input stream of decrypted group data.</param>
    private Stream DecompressGroup(GroupHeader group, Stream inputStream)
    {
        switch (group.CompressedSize)
        {
            case > 0 when Header.Flags.HasFlag(IceFileFlags.Kraken):
            {
                // Kraken decompression
                var scratch = new byte[group.GetStoredSize()];
                var nRead1 = inputStream.Read(scratch, 0, scratch.Length);
                Debug.Assert(nRead1 == scratch.Length, "Decryption gave unexpected decrypted data size.");

                var result = new byte[group.RawSize];
                var nRead2 = Kraken.Decompress(scratch, group.CompressedSize, result, group.RawSize);
                Debug.Assert(nRead2 != -1, "Kraken decompression failed due to an error.");
                Debug.Assert(nRead2 == result.Length, "Kraken decompression gave unexpected uncompressed size.");
                return new MemoryStream(result);
            }
            case > 0:
            {
                // PRS decompression
                var prsInput = new IcePrsInputStream(inputStream);
                var decompressionStream = new PrsStream(prsInput);
                return decompressionStream;
            }
            default:
            {
                // Uncompressed data
                return inputStream;
            }
        }
    }
    
    private static FileEntry[] UnpackGroupHeadersOnly(GroupHeader header, Stream data)
    {
        using var reader = new BinaryReader(data);
        var entries = new FileEntry[header.FileCount];
        for (var i = 0; i < header.FileCount; i++)
        {
            var entryHeader = FileEntryHeader.Read(reader);

            if (i <= header.FileCount - 2)
            {
                reader.Seek(entryHeader.EntrySize - entryHeader.HeaderSize, SeekOrigin.Current);
            }
            
            entries[i] = new FileEntry(entryHeader, Array.Empty<byte>());
        }
        
        return entries;
    }

    private static FileEntry[] UnpackGroup(GroupHeader header, Stream data)
    {
        using var br = new BinaryReader(data);
        return Enumerable.Repeat(br, Convert.ToInt32(header.FileCount))
            .Select(reader =>
            {
                var entryHeader = FileEntryHeader.Read(reader);
                var entryData = reader.ReadBytes(Convert.ToInt32(entryHeader.DataSize));
                // Files are padded to be 16-byte aligned if they aren't naturally like that. I don't
                // know if the game reads this padding as data or not. Zamboni does read this as data.
                var padding = entryHeader.EntrySize - entryHeader.DataSize - entryHeader.HeaderSize;
                reader.Seek(padding, SeekOrigin.Current);
                return new FileEntry(entryHeader, entryData);
            })
            .ToArray();
    }

    private static void GetBlowfishKeys(BlowfishKeys blowfishKeys, byte[] magic, int compressedSize)
    {
        var crc32 = new Crc32();
        crc32.Init();
        crc32.Update(magic, 124, 96);

        var tempKey =
            (uint)((int)crc32.Checksum ^
                   (int)BitConverter.ToUInt32(magic, 108) ^ compressedSize ^ 1129510338);
        var key = GetBlowfishKey(magic, tempKey);
        blowfishKeys.GroupDataKeys[0][0] = CalcBlowfishKey(magic, key);
        blowfishKeys.GroupDataKeys[0][1] = GetBlowfishKey(magic, blowfishKeys.GroupDataKeys[0][0]);
        blowfishKeys.GroupDataKeys[1][0] =
            blowfishKeys.GroupDataKeys[0][0] >> 15 | blowfishKeys.GroupDataKeys[0][0] << 17;
        blowfishKeys.GroupDataKeys[1][1] =
            blowfishKeys.GroupDataKeys[0][1] >> 15 | blowfishKeys.GroupDataKeys[0][1] << 17;

        var x = blowfishKeys.GroupDataKeys[0][0] << 13 | blowfishKeys.GroupDataKeys[0][0] >> 19;
        blowfishKeys.GroupHeadersKey = x;
    }

    private static uint CalcBlowfishKey(IReadOnlyList<byte> magic, uint tempKey)
    {
        var tempKey1 = 2382545500U ^ tempKey;
        var num1 = (uint)(613566757L * tempKey1 >> 32);
        var num2 = ((tempKey1 - num1 >> 1) + num1 >> 2) * 7U;
        for (var index = (int)tempKey1 - (int)num2 + 2; index > 0; --index)
            tempKey1 = GetBlowfishKey(magic, tempKey1);
        return (uint)((int)tempKey1 ^ 1129510338 ^ -850380898);
    }

    private static uint GetBlowfishKey(IReadOnlyList<byte> magic, uint tempKey)
    {
        var num1 = (uint)(((int)tempKey & 0xFF) + 93 & 0xFF);
        var num2 = (uint)((int)(tempKey >> 8) + 63 & 0xFF);
        var num3 = (uint)((int)(tempKey >> 16) + 69 & 0xFF);
        var num4 = (uint)((int)(tempKey >> 24) - 58 & 0xFF);
        return (uint)((byte)((magic[(int)num2] << 7 | magic[(int)num2] >> 1) & 0xFF) << 24 |
                      (byte)((magic[(int)num4] << 6 | magic[(int)num4] >> 2) & 0xFF) << 16 |
                      (byte)((magic[(int)num1] << 5 | magic[(int)num1] >> 3) & 0xFF) << 8) |
               (byte)((magic[(int)num3] << 5 | magic[(int)num3] >> 3) & 0xFF);
    }

    private class BlowfishKeys
    {
        public uint GroupHeadersKey;

        public uint[][] GroupDataKeys { get; }

        public BlowfishKeys()
        {
            GroupDataKeys = new uint[2][];
            GroupDataKeys[0] = new uint[2];
            GroupDataKeys[1] = new uint[2];
        }
    }
}