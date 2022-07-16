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

        // Decrypt the file headers, if necessary
        byte[] group1DataCompressed;
        byte[] group2DataCompressed;
        if (Header.Flags.HasFlag(IceFileFlags.Encrypted))
        {
            var keys = GetBlowfishKeys(Header.BlowfishMagic, Convert.ToInt32(Header.FileSize));
            var headersRaw = Reader.ReadBytes(0x30);
            var blowfish = new Blowfish(BitConverter.GetBytes(keys.GroupHeadersKey));
            blowfish.Decrypt(ref headersRaw);
            
            using var headersMem = new MemoryStream(headersRaw);
            using var subReader = new BinaryReader(headersMem);

            Group1 = GroupHeader.Read(subReader);
            Group2 = GroupHeader.Read(subReader);

            var encryptedGroup1Data = Reader.ReadBytes(Convert.ToInt32(Group1.CompressedSize));
            var encryptedGroup2Data = Reader.ReadBytes(Convert.ToInt32(Group2.CompressedSize));

            group1DataCompressed =
                DecryptGroup(encryptedGroup1Data, keys.Group1Keys[0], keys.Group1Keys[1], false);
            group2DataCompressed =
                DecryptGroup(encryptedGroup2Data, keys.Group2Keys[0], keys.Group2Keys[1], false);
        }
        else
        {
            Group1 = GroupHeader.Read(Reader);
            Group2 = GroupHeader.Read(Reader);
            Reader.Seek(0x10, SeekOrigin.Current);
            group1DataCompressed = Reader.ReadBytes(Convert.ToInt32(Group1.CompressedSize));
            group2DataCompressed = Reader.ReadBytes(Convert.ToInt32(Group2.CompressedSize));
        }

        // Decompress the archive contents
        var group1Data = new byte[Group1.RawSize];
        var group2Data = new byte[Group2.RawSize];
        if (Header.Flags.HasFlag(IceFileFlags.Kraken))
        {
            if (group1DataCompressed.Length > 0)
            {
                Kraken.Decompress(group1DataCompressed, Group1.CompressedSize, group1Data, Group1.RawSize);
            }

            if (group2DataCompressed.Length > 0)
            {
                Kraken.Decompress(group2DataCompressed, Group2.CompressedSize, group2Data, Group2.RawSize);
            }
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
                var entryData = reader.ReadBytes(Convert.ToInt32(entryHeader.DataSize));
                return new FileEntry(entryHeader, entryData);
            })
            .Where(e => e.HasValue)
            .Select(e => e!.Value)
            .ToArray();
    }

    private static int SecondPassThreshold => 102400;

    private static byte[] DecryptGroup(byte[] buffer, uint key1, uint key2, bool v3Decrypt)
    {
        var block = new byte[buffer.Length];
        if (!v3Decrypt)
        {
            block = FloatageFish.DecryptBlock(buffer, (uint)buffer.Length, key1, 16);
        }
        else
        {
            Array.Copy(buffer, 0, block, 0, buffer.Length);
        }
        
        // The encrypted buffer must be a multiple of 8 bytes long for Blowfish
        // decryption, but FloatageFish requires that the buffer have its original
        // length. This means we need to copy the data here; a zero-copy reimplementation
        // of FloatageFish may remove the need for this copy.
        var blockPadded = new byte[block.Length + (8 - block.Length % 8)];
        Array.Copy(block, 0, blockPadded, 0, block.Length);
        
        var blowfish1 = new Blowfish(BitConverter.GetBytes(key1));
        var blowfish2 = new Blowfish(BitConverter.GetBytes(key2));
        
        blowfish1.Decrypt(ref blockPadded);
        if (block.Length <= SecondPassThreshold && v3Decrypt == false)
            blowfish2.Decrypt(ref blockPadded);
        
        // Copy the data back to the original array with its correct size
        Array.Copy(blockPadded, 0, block, 0, block.Length);
        
        return block;
    }

    private static BlowfishKeys GetBlowfishKeys(byte[] magic, int compressedSize)
    {
        var blowfishKeys = new BlowfishKeys();

        var crc32 = new Crc32();
        crc32.Init();
        crc32.Update(magic, 124, 96);

        var tempKey =
            (uint)((int)crc32.Checksum ^
                   (int)BitConverter.ToUInt32(magic, 108) ^ compressedSize ^ 1129510338);
        var key = GetBlowfishKey(magic, tempKey);
        blowfishKeys.Group1Keys[0] = CalcBlowfishKey(magic, key);
        blowfishKeys.Group1Keys[1] = GetBlowfishKey(magic, blowfishKeys.Group1Keys[0]);
        blowfishKeys.Group2Keys[0] = blowfishKeys.Group1Keys[0] >> 15 | blowfishKeys.Group1Keys[0] << 17;
        blowfishKeys.Group2Keys[1] = blowfishKeys.Group1Keys[1] >> 15 | blowfishKeys.Group1Keys[1] << 17;

        var x = blowfishKeys.Group1Keys[0] << 13 | blowfishKeys.Group1Keys[0] >> 19;
        blowfishKeys.GroupHeadersKey = BitConverter.ToUInt32(BitConverter.GetBytes(x));

        return blowfishKeys;
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

        public uint[] Group1Keys { get; } = new uint[2];

        public uint[] Group2Keys { get; } = new uint[2];
    }
}