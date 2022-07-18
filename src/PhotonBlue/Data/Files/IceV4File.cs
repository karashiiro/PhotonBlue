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
        byte[] group1DataRaw;
        byte[] group2DataRaw;
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

            group1DataRaw = Reader.ReadBytes(Convert.ToInt32(Group1.GetStoredSize()));
            group2DataRaw = Reader.ReadBytes(Convert.ToInt32(Group2.GetStoredSize()));

            if (group1DataRaw.Length > 0)
            {
                DecryptGroup(group1DataRaw, keys.Group1Keys[0], keys.Group1Keys[1]);
            }

            if (group2DataRaw.Length > 0)
            {
                DecryptGroup(group2DataRaw, keys.Group2Keys[0], keys.Group2Keys[1]);
            }
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
        var group1Data = HandleGroupDecompression(Group1, group1DataRaw);
        var group2Data = HandleGroupDecompression(Group2, group2DataRaw);

        Group1Entries = UnpackGroup(Group1, group1Data);
        Group2Entries = UnpackGroup(Group2, group2Data);
    }

    private byte[] HandleGroupDecompression(GroupHeader group, byte[] data)
    {
        if (group.CompressedSize > 0)
        {
            var result = new byte[group.RawSize];
            DecompressGroup(group, data, result);
            return result;
        }
        
        return data;
    }

    private void DecompressGroup(GroupHeader group, byte[] compressed, byte[] result)
    {
        if (Header.Flags.HasFlag(IceFileFlags.Kraken))
        {
            var nRead = Kraken.Decompress(compressed, group.CompressedSize, result, group.RawSize);
            Debug.Assert(nRead == result.Length);
        }
        else
        {
            using var mem = new MemoryStream(compressed);
            using var prsInput = new IcePrsInputStream(mem);
            using var decompressionStream = new PrsStream(prsInput);
            var nRead = decompressionStream.Read(result, 0, result.Length);
            Debug.Assert(nRead == result.Length);
        }
    }

    private static FileEntry[] UnpackGroup(GroupHeader header, byte[] data)
    {
        using var buf = new MemoryStream(data);
        using var br = new BinaryReader(buf);
        return Enumerable.Repeat(br, Convert.ToInt32(header.FileCount))
            .Select(reader =>
            {
                var basePos = reader.BaseStream.Position;
                var entryHeader = FileEntryHeader.Read(reader);
                // There seems to sometimes be a gap between the entry header and the entry data that isn't
                // accounted for any documentation. It doesn't seem to be related to the file name length,
                // but I may be wrong. This occurs in win32/0000064b91444b04df5d95f6a0bc55be.
                reader.Seek(basePos + (entryHeader.FileSize - entryHeader.DataSize), SeekOrigin.Begin);
                var entryData = reader.ReadBytes(Convert.ToInt32(entryHeader.DataSize));
                return new FileEntry(entryHeader, entryData);
            })
            .ToArray();
    }

    private const int SecondPassThreshold = 102400;

    private static void DecryptGroup(byte[] buffer, uint key1, uint key2)
    {
        FloatageFish.DecryptBlock(buffer, (uint)buffer.Length, key1, 16);
        
        var blowfish1 = new Blowfish(BitConverter.GetBytes(key1));
        var blowfish2 = new Blowfish(BitConverter.GetBytes(key2));
        
        blowfish1.Decrypt(ref buffer);
        if (buffer.Length <= SecondPassThreshold)
        {
            blowfish2.Decrypt(ref buffer);
        }
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
        blowfishKeys.GroupHeadersKey = x;

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