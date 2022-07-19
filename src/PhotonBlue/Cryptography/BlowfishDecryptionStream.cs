﻿using System.Diagnostics;

namespace PhotonBlue.Cryptography;

public class BlowfishDecryptionStream : Stream
{
    public override bool CanRead => _stream.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _stream.Length;

    public override long Position
    {
        get => _stream.Position;
        set => _stream.Position = value;
    }

    private int HoldCount => _holdEnd - _holdStart;

    // An internal buffer for holding decrypted data that the consumer
    // doesn't want to read, yet. Because Blowfish decrypts blocks 8 bytes
    // at a time, this will never be larger than 8 bytes.
    private readonly byte[] _hold;
    private int _holdStart;
    private int _holdEnd;

    private readonly Blowfish _blowfish;
    private readonly Stream _stream;

    public BlowfishDecryptionStream(Stream data, IEnumerable<byte> key)
    {
        _hold = new byte[8];
        _holdStart = 0;
        _holdEnd = 0;
        _blowfish = new Blowfish(key);
        _stream = data;
    }

    public override void Flush()
    {
        _stream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (count == 0)
        {
            return 0;
        }

        var outIndex = offset;

        // Load any held decrypted bytes from previous operations into the output buffer.
        var nLeft = count;
        if (HoldCount != 0)
        {
            var nCopied = Math.Min(HoldCount, count);
            LoadHeldBytes(buffer, offset, nCopied);
            outIndex += nCopied;
            nLeft -= nCopied;
        }

        // Create a padded buffer that can hold a multiple of 8 bytes for decryption, and
        // fill it with data. If this would read more data than the amount of remaining data,
        // then just read all of the remaining data.
        var nToRead = Convert.ToInt32(Math.Min(nLeft + nLeft % 8, _stream.Length - _stream.Position));
        var readBuf = new byte[nToRead]; // Not sure how to avoid this allocation
        var nRead = _stream.Read(readBuf, 0, nToRead);

        // No data remaining; return early.
        if (nRead == 0)
        {
            return outIndex - offset;
        }

        if (_stream.Length % 8 == 0 || _stream.Length - _stream.Position >= 8)
        {
            // Standard Blowfish needs its payloads to be a multiple of 8 bytes long.
            // When we're reading data before the end of the payload, we follow standard
            // Blowfish conventions.
            _blowfish.DecryptStandard(ref readBuf);
        }
        else
        {
            // If we're reading data at the end of the payload, we degrade to SEGA's
            // broken Blowfish implementation, which ignores the last few bytes.
            _blowfish.Decrypt(ref readBuf);
        }

        // Copy the decrypted data to the appropriate arrays.
        var toHold = Math.Max(nRead - nLeft, 0);
        var toCopy = nRead - toHold;
        Array.Copy(readBuf, 0, buffer, outIndex, toCopy);
        outIndex += toCopy;
        if (toHold > 0)
        {
            HoldBytes(readBuf, nLeft, toHold);
        }

        // We lie about the returned number of bytes and only inform the consumer that
        // we have read up to the requested amount of data. In reality, we may have
        // read more than that and stored it in our internal buffer.
        return Math.Min(outIndex - offset, count);
    }

    private void LoadHeldBytes(byte[] buffer, int offset, int count)
    {
        Debug.Assert(count > 0, "Tried to read negative amount of bytes from the hold buffer.");
        Debug.Assert(HoldCount - count >= 0, "Hold buffer does not contain enough elements to support this operation.");
        Array.Copy(_hold, _holdStart, buffer, offset, count);
        _holdStart += count;
    }

    private void HoldBytes(byte[] data, int offset, int count)
    {
        Debug.Assert(HoldCount == 0, "Hold buffer still contains unread data.");
        Debug.Assert(count > 0, "Tried to store negative amount of bytes in the hold buffer.");
        Debug.Assert(count <= 8, "Hold buffer would be larger than 8 bytes after this operation.");

        // Clobber any data currently in the hold buffer since this will never be called
        // while the hold buffer still has data.
        Array.Copy(data, offset, _hold, 0, count);
        _holdStart = 0;
        _holdEnd = count;
    }

    public override int ReadByte()
    {
        // Read data from the internal buffer.
        if (HoldCount != 0)
        {
            return _hold[_holdStart++];
        }

        // If the underlying data is not a multiple of 8 bytes long and in the
        // remainder bytes, then just return data without decrypting it. This
        // is an artifact of SEGA's interesting Blowfish implementation.
        if (_stream.Length % 8 != 0 && _stream.Length - _stream.Position < 8)
        {
            return _stream.ReadByte();
        }

        // Otherwise, read data from the stream, decrypt it normally, and hold
        // anything we're not returning.
        var buf = new byte[8];
        var nRead = _stream.Read(buf, 0, buf.Length);

        _blowfish.DecryptStandard(ref buf);
        HoldBytes(buf, 1, nRead - 1);

        // Return the next decrypted byte.
        return buf[0];
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        // This can't meaningfully be implemented. Seeking to a point in the
        // file requires decrypting everything before it so that the Blowfish
        // tables are in the correct state to continue reading data.
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        _stream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }
}