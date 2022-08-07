using System.Buffers;
using System.Diagnostics;
using PhotonBlue.Attributes;
using PhotonBlue.Cryptography;
using PhotonBlue.Extensions;
using PhotonBlue.Ooz;

namespace PhotonBlue.Data.Files;

[FileMagic("ICE")]
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
            return new GroupHeader
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

    public IceV4File()
    {
        _keys = new BlowfishKeys();

        Group1Entries = Array.Empty<FileEntry>();
        Group2Entries = Array.Empty<FileEntry>();
    }

    public IceV4File(Stream data, IObjectPool<BlowfishGpuHandle, Blowfish> blowfishGpuPool) : base(data, blowfishGpuPool)
    {
        _keys = new BlowfishKeys();

        Group1Entries = Array.Empty<FileEntry>();
        Group2Entries = Array.Empty<FileEntry>();
    }

    public override void LoadFile()
    {
        // Read the ICE archive headers
        base.LoadFile();
        if (Header.Version != 4)
        {
            throw new InvalidOperationException($"Incorrect ICE version detected; expected 4, got {Header.Version}.");
        }

        LoadGroupHeaders();

        Debug.Assert(Reader != null);

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

    public override void LoadHeadersOnly()
    {
        // Read the ICE archive headers
        base.LoadHeadersOnly();
        if (Header.Version != 4)
        {
            throw new InvalidOperationException($"Incorrect ICE version detected; expected 4, got {Header.Version}.");
        }

        LoadGroupHeaders();

        Debug.Assert(Reader != null);

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

    private void LoadGroupHeaders()
    {
        Debug.Assert(Reader != null);

        // Decrypt the file headers, if necessary
        _keys = new BlowfishKeys();
        if (Header.Flags.HasFlag(IceFileFlags.Encrypted))
        {
            GetBlowfishKeys(_keys, Header.BlowfishMagic, Convert.ToInt32(Header.FileSize));
            var headersRaw = Reader.ReadBytes(0x30);
            using var blowfish = new Blowfish(BitConverter.GetBytes(_keys.GroupHeadersKey));
            blowfish.Decrypt(headersRaw);

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
        var group1FileCount = Convert.ToInt32(Group1.FileCount);
        var group2FileCount = Convert.ToInt32(Group2.FileCount);
        var totalFiles = group1FileCount + group2FileCount;

        // Extract the first group
        var (group1Stream, junk1) = HandleGroupExtraction(Group1, _keys.Group1DataKey1, _keys.Group1DataKey2, partitionedStream);
        using var junk1Deferred = MicroDisposable<DisposableBundle>.Create(junk1, o => o.Dispose());

        Group1Entries = UnpackGroupHeadersOnly(0, group1FileCount, totalFiles, group1Stream);

        if (Group2.FileCount > 0)
        {
            // Move to the group 2 partition
            partitionedStream.NextPartition();

            // Extract the next group
            var (group2Stream, junk2) = HandleGroupExtraction(Group2, _keys.Group2DataKey1, _keys.Group2DataKey2, partitionedStream);
            using var junk2Deferred = MicroDisposable<DisposableBundle>.Create(junk2, o => o.Dispose());

            Group2Entries = UnpackGroupHeadersOnly(group1FileCount, group2FileCount, totalFiles, group2Stream);
        }
    }

    private void LoadFileEntries(PartitionedStream partitionedStream)
    {
        // Extract the first group
        var (group1Stream, junk1) = HandleGroupExtraction(Group1, _keys.Group1DataKey1, _keys.Group1DataKey2, partitionedStream);
        using var junk1Deferred = MicroDisposable<DisposableBundle>.Create(junk1, o => o.Dispose());

        Group1Entries = UnpackGroup(Group1, group1Stream);

        // Move to the group 2 partition
        partitionedStream.NextPartition();

        // Extract the next group
        var (group2Stream, junk2) = HandleGroupExtraction(Group2, _keys.Group2DataKey1, _keys.Group2DataKey2, partitionedStream);
        using var junk2Deferred = MicroDisposable<DisposableBundle>.Create(junk2, o => o.Dispose());

        Group2Entries = UnpackGroup(Group2, group2Stream);
    }

    private (Stream, DisposableBundle) HandleGroupExtraction(GroupHeader group, uint key1, uint key2, Stream data)
    {
        return data.Length == 0 ? (data, DisposableBundle.Empty) : DecodeGroup(group, key1, key2, data);
    }

    private const int SecondPassThreshold = 102400;

    /// <summary>
    /// Decrypts and decompresses an ICE group.
    /// </summary>
    /// <param name="group">The group header.</param>
    /// <param name="key1">The group's first decryption key, if the file is encrypted.</param>
    /// <param name="key2">The group's first decryption key, if the file is encrypted.</param>
    /// <param name="data">The raw group data.</param>
    private (Stream, DisposableBundle) DecodeGroup(GroupHeader group, uint key1, uint key2, Stream data)
    {
        var (decryptStream, junk) = PrepareGroupDecryption(data, key1, key2);
        return (DecompressGroup(group, decryptStream, junk), junk);
    }

    /// <summary>
    /// Prepares a decryption stream over the provided encrypted group data stream.
    /// </summary>
    /// <param name="data">The group data stream.</param>
    /// <param name="key1">The group's first decryption key, if the file is encrypted.</param>
    /// <param name="key2">The group's first decryption key, if the file is encrypted.</param>
    /// <returns></returns>
    private (Stream, DisposableBundle) PrepareGroupDecryption(Stream data, uint key1, uint key2)
    {
        var junk = new DisposableBundle();

        var decryptStream = data;
        if (Header.Flags.HasFlag(IceFileFlags.Encrypted))
        {
            var floatageFish = new FloatageFishDecryptionStream(data, key1, 16);

            // These need to get disposed, so we add them to the junk pile.
            Debug.Assert(BlowfishGpuPool != null, nameof(BlowfishGpuPool) + " != null");
            decryptStream = new BlowfishDecryptionStream(BlowfishGpuPool, floatageFish, BitConverter.GetBytes(key1));
            junk.Objects.Add(decryptStream);

            if (data.Length <= SecondPassThreshold)
            {
                decryptStream = new BlowfishDecryptionStream(BlowfishGpuPool, decryptStream, BitConverter.GetBytes(key2));
                junk.Objects.Add(decryptStream);
            }
        }

        return (decryptStream, junk);
    }

    /// <summary>
    /// Decompresses an ICE group.
    /// </summary>
    /// <param name="group">The group header.</param>
    /// <param name="inputStream">The input stream of decrypted group data.</param>
    /// <param name="junk">A bundle of junk to be disposed of at the end of the extraction process.</param>
    private Stream DecompressGroup(GroupHeader group, Stream inputStream, DisposableBundle junk)
    {
        switch (group.CompressedSize)
        {
            case > 0 when Header.Flags.HasFlag(IceFileFlags.Kraken):
            {
                // Ideally, Kraken decompression could be performed in chunks for performance, but that
                // would take a major refactor of ooz, which I really don't feel like doing. Instead,
                // we just handle the case where the buffers we need are larger than what the array pool
                // is optimized for by allocating our own arrays. Renting arrays larger than the max size
                // incurs a performance hit when returning the array to the pool.
                const int arrayPoolMax = 1048576;

                // Kraken decompression
                var scratchSize = Convert.ToInt32(group.GetStoredSize());
                var scratchPooled = scratchSize <= arrayPoolMax;
                var scratch = scratchPooled ? ArrayPool<byte>.Shared.Rent(scratchSize) : new byte[scratchSize];
                if (scratchPooled)
                {
                    junk.Objects.Add(MicroDisposable<byte[]>.Create(scratch, o => ArrayPool<byte>.Shared.Return(o)));
                }

                var nRead1 = inputStream.Read(scratch, 0, scratchSize);
                Debug.Assert(nRead1 == scratchSize, "Decryption gave unexpected decrypted data size.");

                var resultSize = Convert.ToInt32(group.RawSize);
                var resultPooled = resultSize <= arrayPoolMax;
                var result = resultPooled ? ArrayPool<byte>.Shared.Rent(resultSize) : new byte[resultSize];
                if (resultPooled)
                {
                    junk.Objects.Add(MicroDisposable<byte[]>.Create(result, o => ArrayPool<byte>.Shared.Return(o)));
                }

                var nRead2 = Kraken.Decompress(scratch, group.CompressedSize, result, group.RawSize);
                Debug.Assert(nRead2 != -1, "Kraken decompression failed due to an error.");
                Debug.Assert(nRead2 == resultSize, "Kraken decompression gave unexpected uncompressed size.");

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

    private static FileEntry[] UnpackGroupHeadersOnly(int startFile, int endFile, int totalFiles, Stream data)
    {
        using var br = new BinaryReader(data);
        return Enumerable.Range(startFile, endFile)
            .Select(n =>
            {
                var entryHeader = FileEntryHeader.Read(br);
                // If this is the last file in the archive, we can just ignore its data entirely.
                if (n < totalFiles - 1)
                    br.Seek(entryHeader.EntrySize - entryHeader.HeaderSize, SeekOrigin.Current);
                return new FileEntry(entryHeader);
            })
            .ToArray();
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
        blowfishKeys.Group1DataKey1 = CalcBlowfishKey(magic, key);
        blowfishKeys.Group1DataKey2 = GetBlowfishKey(magic, blowfishKeys.Group1DataKey1);
        blowfishKeys.Group2DataKey1 =
            blowfishKeys.Group1DataKey1 >> 15 | blowfishKeys.Group1DataKey1 << 17;
        blowfishKeys.Group2DataKey2 =
            blowfishKeys.Group1DataKey2 >> 15 | blowfishKeys.Group1DataKey2 << 17;

        var x = blowfishKeys.Group1DataKey1 << 13 | blowfishKeys.Group1DataKey1 >> 19;
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
        public uint Group1DataKey1;
        public uint Group1DataKey2;
        public uint Group2DataKey1;
        public uint Group2DataKey2;
    }
}