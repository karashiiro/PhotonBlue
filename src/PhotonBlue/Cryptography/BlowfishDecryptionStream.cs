using System.Diagnostics;

namespace PhotonBlue.Cryptography;

internal sealed class BlowfishDecryptionStream : Stream
{
    public override bool CanRead => _stream.CanRead;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    
    // ReSharper disable once ConvertToAutoPropertyWithPrivateSetter
    public override long Length => _length;
    public override long Position
    {
        get => _position - HoldCount;
        set => throw new NotSupportedException();
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

    private long _length;
    private long _position;

    public BlowfishDecryptionStream(Stream data, IEnumerable<byte> key)
    {
        _hold = new byte[8];
        _holdStart = 0;
        _holdEnd = 0;
        
        _blowfish = new Blowfish(key);
        _stream = data;

        _length = data.Length;
        _position = data.Position;
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
            LoadHeldBytes(buffer.AsSpan(offset, nCopied));
            outIndex += nCopied;
            nLeft -= nCopied;
        }

        if (nLeft == 0)
        {
            // All requested data was retrieved from the hold buffer.
            return outIndex - offset;
        }

        // Create a padded buffer that can hold a multiple of 8 bytes for decryption, and
        // fill it with data. If this would read more data than the amount of remaining data,
        // then just read all of the remaining data.
        var nToRead = Convert.ToInt32(Math.Min(nLeft + (8 - nLeft % 8), _length - _position));
        var readBuf = new byte[nToRead]; // Not sure how to avoid this allocation
        var nRead = _stream.Read(readBuf, 0, nToRead);
        _position += nRead;

        // No data remaining; return early.
        if (nRead == 0)
        {
            return outIndex - offset;
        }

        if (_length % 8 == 0 || _length - _position >= 8)
        {
            // Standard Blowfish needs its payloads to be a multiple of 8 bytes long.
            // When we're reading data before the end of the payload, we follow standard
            // Blowfish conventions.
            _blowfish.DecryptStandard(readBuf);
        }
        else
        {
            // If we're reading data at the end of the payload, we degrade to SEGA's
            // broken Blowfish implementation, which ignores the last few bytes.
            _blowfish.Decrypt(readBuf);
        }

        // Copy the decrypted data to the appropriate arrays.
        var toHold = Math.Max(nRead - nLeft, 0);
        var toCopy = nRead - toHold;
        Array.Copy(readBuf, 0, buffer, outIndex, toCopy);
        outIndex += toCopy;
        if (toHold > 0)
        {
            HoldBytes(readBuf.AsSpan(nLeft, toHold));
        }

        // We lie about the returned number of bytes and only inform the consumer that
        // we have read up to the requested amount of data. In reality, we may have
        // read more than that and stored it in our internal buffer.
        return Math.Min(outIndex - offset, count);
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
        if (_length % 8 != 0 && _length - _position < 8)
        {
            _position++;
            return _stream.ReadByte();
        }

        // Otherwise, read data from the stream, decrypt it normally, and hold
        // anything we're not returning. We can read data directly into the hold
        // buffer, since we know that it has been completely consumed. We also
        // know that this is not the end of the file, so we don't need to handle
        // the buffer being only partially filled.
        var nRead = _stream.Read(_hold, 0, _hold.Length);
        _position += nRead;
        Debug.Assert(nRead == _hold.Length, "Unexpected end of stream.");

        _holdStart = 0;
        _holdEnd = nRead;

        _blowfish.DecryptStandard(_hold);

        // Return the next decrypted byte, if possible.
        if (HoldCount != 0)
        {
            return _hold[_holdStart++];
        }

        return -1;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var absoluteOffset = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => offset + Position,
            SeekOrigin.End => _length - offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null),
        };

        var count = absoluteOffset - Position;
        Span<byte> junk1 = stackalloc byte[Math.Min(HoldCount, Convert.ToInt32(count))];
        count -= HoldCount;
        LoadHeldBytes(junk1);

        if (count <= 0)
        {
            return Position;
        }
        
        Debug.Assert(HoldCount == 0, "Hold buffer still contains unread data.");
        
        var alignedCount = count - count % 8;
        _stream.Seek(alignedCount, SeekOrigin.Current);
        if (count - alignedCount == 0)
        {
            return Position;
        }

        // This will be at most 16 bytes long.
        Span<byte> junk2 = stackalloc byte[Convert.ToInt32(count - alignedCount)];
        var nRead = Read(junk2);
        Debug.Assert(nRead == junk2.Length, "Unexpected end of stream.");
        
        return Position;
    }

    public override void SetLength(long value)
    {
        _stream.SetLength(value);
        _length = value;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }
    
    private void LoadHeldBytes(Span<byte> buffer)
    {
        Debug.Assert(HoldCount - buffer.Length >= 0, "Hold buffer does not contain enough elements to support this operation.");
        _hold.AsSpan(_holdStart, buffer.Length).CopyTo(buffer);
        _holdStart += buffer.Length;
    }

    private void HoldBytes(Span<byte> buffer)
    {
        Debug.Assert(HoldCount == 0, "Hold buffer still contains unread data.");
        Debug.Assert(buffer.Length <= 8, "Hold buffer would be larger than 8 bytes after this operation.");

        // Clobber any data currently in the hold buffer since this will never be called
        // while the hold buffer still has data.
        buffer.CopyTo(_hold.AsSpan(0, buffer.Length));
        _holdStart = 0;
        _holdEnd = buffer.Length;
    }
}