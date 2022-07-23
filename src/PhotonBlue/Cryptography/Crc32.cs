﻿namespace PhotonBlue.Cryptography;

internal sealed class Crc32
{
    private const uint Poly = 0xedb88320;

    private static readonly uint[] CrcArray =
        Enumerable.Range(0, 256)
            .Select(i =>
            {
                var k = (uint)i;
                for (var j = 0; j < 8; j++)
                    k = (k & 1) != 0 ? (k >> 1) ^ Poly : k >> 1;

                return k;
            })
            .ToArray();

    public uint Checksum => ~_crc32;

    private uint _crc32 = 0xFFFFFFFF;

    /// <summary>
    /// Initializes Crc32's state
    /// </summary>
    public void Init()
    {
        _crc32 = 0xFFFFFFFF;
    }

    /// <summary>
    /// Updates Crc32's state with new data
    /// </summary>
    /// <param name="data">Data to calculate the new CRC from</param>
    public void Update(IEnumerable<byte> data)
    {
        foreach (var b in data)
            Update(b);
    }

    public void Update(byte[] data, int offset, int length)
    {
        for (int i = offset, readIndex = offset + length; i < readIndex; i++)
            Update(data[i]);
    }

    public void Update(byte b)
    {
        _crc32 = CrcArray[(_crc32 ^ b) & 0xFF] ^ ((_crc32 >> 8) & 0x00FFFFFF);
    }

    public static uint Calculate(byte[] data, int offset, int length)
    {
        var v = 0xFFFFFFFF;
        for (int i = offset, readIndex = offset + length; i < readIndex; i++)
            v = CrcArray[(v ^ data[i]) & 0xFF] ^ ((v >> 8) & 0x00FFFFFF);
        return ~v;
    }
}