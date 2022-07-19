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

    public IceV4File(Stream data) : base(data)
    {
        Group1Entries = Array.Empty<FileEntry>();
        Group2Entries = Array.Empty<FileEntry>();
    }

    public override void LoadFile()
    {
        // Read the ICE archive header
        base.LoadFile();

        Debug.Assert(Header.Version == 4, "Incorrect ICE version detected!");

        // Decrypt the file headers, if necessary
        var keys = new BlowfishKeys();
        byte[] group1DataRaw;
        byte[] group2DataRaw;
        if (Header.Flags.HasFlag(IceFileFlags.Encrypted))
        {
            GetBlowfishKeys(keys, Header.BlowfishMagic, Convert.ToInt32(Header.FileSize));
            var headersRaw = Reader.ReadBytes(0x30);
            var blowfish = new Blowfish(BitConverter.GetBytes(keys.GroupHeadersKey));
            blowfish.Decrypt(ref headersRaw);

            using var headersMem = new MemoryStream(headersRaw);
            using var subReader = new BinaryReader(headersMem);

            Group1 = GroupHeader.Read(subReader);
            Group2 = GroupHeader.Read(subReader);

            group1DataRaw = Reader.ReadBytes(Convert.ToInt32(Group1.GetStoredSize()));
            group2DataRaw = Reader.ReadBytes(Convert.ToInt32(Group2.GetStoredSize()));
        }
        else
        {
            Group1 = GroupHeader.Read(Reader);
            Group2 = GroupHeader.Read(Reader);
            Reader.Seek(0x10, SeekOrigin.Current);
            group1DataRaw = Reader.ReadBytes(Convert.ToInt32(Group1.GetStoredSize()));
            group2DataRaw = Reader.ReadBytes(Convert.ToInt32(Group2.GetStoredSize()));
        }

        // Decompress the archive contents
        var group1Data = HandleGroupExtraction(Group1, keys.GroupDataKeys[0], group1DataRaw);
        var group2Data = HandleGroupExtraction(Group2, keys.GroupDataKeys[1], group2DataRaw);

        Group1Entries = UnpackGroup(Group1, group1Data);
        Group2Entries = UnpackGroup(Group2, group2Data);
    }

    private byte[] HandleGroupExtraction(GroupHeader group, IReadOnlyList<uint> keys, byte[] data)
    {
        if (data.Length == 0)
        {
            return data;
        }

        // We only need to allocate a new array if we need to decompress the data; otherwise
        // we can decode it in-place.
        var result = group.CompressedSize > 0 ? new byte[group.RawSize] : data;
        DecodeGroup(group, keys, data, result);
        return result;
    }
    
    private const int SecondPassThreshold = 102400;

    /// <summary>
    /// Decrypts and decompresses an ICE group.
    /// </summary>
    /// <param name="group">The group header.</param>
    /// <param name="keys">The group's decryption keys, if the file is encrypted.</param>
    /// <param name="data">The raw group data.</param>
    /// <param name="result">The buffer that should contain the result data.</param>
    private void DecodeGroup(GroupHeader group, IReadOnlyList<uint> keys, byte[] data, byte[] result)
    {
        using var mem = new MemoryStream(data);
        var decryptStream = PrepareGroupDecryption(mem, keys);
        DecompressGroup(group, decryptStream, data, result);
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
    /// <param name="scratch">An array of a size equal to the input data, used for reading in temporary data.</param>
    /// <param name="result">The buffer that should contain the result data.</param>
    private void DecompressGroup(GroupHeader group, Stream inputStream, byte[] scratch, byte[] result)
    {
        switch (group.CompressedSize)
        {
            case > 0 when Header.Flags.HasFlag(IceFileFlags.Kraken):
            {
                // Kraken decompression
                var nRead1 = inputStream.Read(scratch, 0, scratch.Length);
                Debug.Assert(nRead1 == scratch.Length, "Decryption gave unexpected decrypted data size.");
            
                var nRead2 = Kraken.Decompress(scratch, group.CompressedSize, result, group.RawSize);
                Debug.Assert(nRead2 != -1, "Kraken decompression failed due to an error.");
                Debug.Assert(nRead2 == result.Length, "Kraken decompression gave unexpected uncompressed size.");
                break;
            }
            case > 0:
            {
                // PRS decompression
                var prsInput = new IcePrsInputStream(inputStream);
                var decompressionStream = new PrsStream(prsInput);
                var nRead = decompressionStream.Read(result, 0, result.Length);
                Debug.Assert(nRead == result.Length, "PRS decompression gave unexpected uncompressed size.");
                break;
            }
            default:
            {
                // Uncompressed data
                var nRead = inputStream.Read(result, 0, result.Length);
                Debug.Assert(nRead == result.Length, "Decryption gave unexpected decrypted data size.");
                break;
            }
        }
    }

    private static FileEntry[] UnpackGroup(GroupHeader header, byte[] data)
    {
        using var buf = new MemoryStream(data);
        using var br = new BinaryReader(buf);
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