using System.Diagnostics;

namespace PhotonBlue.Data;

public class PrsStream : Stream
{
    private class PrsPointerState
    {
        public int LoadIndex { get; set; }
        public int BytesRead { get; set; }
        public int Size { get; init; }

        public int Read(IList<byte> lookaround, int lookaroundOffset, byte[] buffer, int bufferIndex,
            int bufferCount)
        {
            var toRead = Math.Min(bufferCount, Size - BytesRead);
            var copyTarget = buffer.AsSpan(bufferIndex, toRead);
            for (var i = 0; i < copyTarget.Length; i++)
            {
                // Read data from the lookaround buffer.
                copyTarget[i] = lookaround[LoadIndex];

                // Read through to the lookaround buffer.
                lookaround[lookaroundOffset++] = lookaround[LoadIndex++];
                LoadIndex %= lookaround.Count;
                lookaroundOffset %= lookaround.Count;
            }
            
            BytesRead += toRead;
            return toRead;
        }

        public int Skip(IList<byte> lookaround, int lookaroundOffset, int count)
        {
            var toRead = Math.Min(count, Size - BytesRead);
            BytesRead += toRead;
            for (var i = 0; i < toRead; i++)
            {
                lookaround[lookaroundOffset++] = lookaround[LoadIndex++];
                LoadIndex %= lookaround.Count;
                lookaroundOffset %= lookaround.Count;
            }

            return toRead;
        }
    }

    private abstract class PrsInstruction
    {
    }
    
    private class PrsEof : PrsInstruction
    {
    }
    
    private class PrsLiteral : PrsInstruction
    {
    }

    private class PrsPointer : PrsInstruction
    {
        public int Offset { get; }
        public int Size { get; }

        public PrsPointer(int offset, int size)
        {
            Offset = offset;
            Size = size;
        }
    }

    private const int MinLongCopyLength = 10;

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }
    
    private byte _ctrlByte;
    private int _ctrlByteCounter;

    // An internal buffer for maintaining some of the decompressed data
    // between reads. This is used as a ring queue.
    private readonly byte[] _lookaround;
    private int _lookaroundIndex;

    // A reference value for ensuring that our lookaround index is synchronized
    // with the data that we want to read. This is only used for debugging.
    private int _bytesRead;

    // An object used for tracking interrupted instructions.
    private PrsPointerState? _currentInstruction;

    private readonly Stream _stream;

    public PrsStream(Stream input)
    {
        _ctrlByteCounter = 8;
        _ctrlByte = 0;
        
        _lookaround = new byte[0x1FFF];
        _lookaroundIndex = 0;
        _bytesRead = 0;
        
        _stream = input;
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (count == 0)
        {
            return 0;
        }

        var outIndex = offset;
        var endIndex = offset + count;

        if (_currentInstruction != null)
        {
            // Resume pending instruction
            var nRead = _currentInstruction.Read(_lookaround, _lookaroundIndex, buffer, outIndex, count);
            outIndex += nRead;
            _lookaroundIndex = (_lookaroundIndex + nRead) % _lookaround.Length;
            _bytesRead += nRead;

            if (_currentInstruction.BytesRead == _currentInstruction.Size)
            {
                _currentInstruction = null;
            }

            if (outIndex == endIndex)
            {
                return outIndex - offset;
            }
        }

        while (outIndex < endIndex)
        {
            var inst = GetNextInstruction();
            switch (inst)
            {
                case PrsEof:
                    return outIndex - offset;
                case PrsLiteral:
                    // Raw data read
                    buffer[outIndex] = (byte)GetNextByte();

                    // Every byte that is read to the output buffer also needs to be read into
                    // the lookaround. This allows for incremental decompression, which is important
                    // for minimizing the amount of work done during file indexing.
                    _lookaround[_lookaroundIndex++] = buffer[outIndex++];
                    _lookaroundIndex %= _lookaround.Length;
                    _bytesRead++;
                    break;
                case PrsPointer ptr:
                {
                    var toRead = Math.Min(ptr.Size, endIndex - outIndex);
                    var copyTarget = buffer.AsSpan(outIndex, toRead);
                    ReadPointer(ptr, copyTarget, toRead);
                    outIndex += toRead;
                    break;
                }
            }
        }

        return outIndex - offset;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        // This is a poor implementation, but it correctly manages the lookaround buffer's state.
        var restrictedOffset = origin switch
        {
            SeekOrigin.Begin => throw new NotSupportedException(),
            SeekOrigin.Current => offset,
            SeekOrigin.End => throw new NotSupportedException(),
            _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null),
        };

        Debug.Assert(restrictedOffset >= 0, "This stream does not support seeking to consumed data.");

        var count = Convert.ToInt32(restrictedOffset);
        var outIndex = 0;
        if (_currentInstruction != null)
        {
            // Resume pending instruction
            var nRead = _currentInstruction.Skip(_lookaround, _lookaroundIndex, count);
            outIndex += nRead;
            _lookaroundIndex = (_lookaroundIndex + nRead) % _lookaround.Length;
            _bytesRead += nRead;

            if (_currentInstruction.BytesRead == _currentInstruction.Size)
            {
                _currentInstruction = null;
            }

            if (outIndex == count)
            {
                return outIndex;
            }
        }

        while (outIndex < count)
        {
            var inst = GetNextInstruction();
            switch (inst)
            {
                case PrsEof:
                    return outIndex - offset;
                case PrsLiteral:
                    _lookaround[_lookaroundIndex++] = (byte)GetNextByte();
                    _lookaroundIndex %= _lookaround.Length;
                    _bytesRead++;
                    outIndex++;
                    break;
                case PrsPointer ptr:
                {
                    var toSeek = Math.Min(ptr.Size, count - outIndex);
                    SeekPointer(ptr, toSeek);
                    outIndex += toSeek;
                    break;
                }
            }
        }

        return outIndex;
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    private void ReadPointer(PrsPointer ptr, Span<byte> buffer, int toRead)
    {
        Debug.Assert(ptr.Offset != 0 && _bytesRead >= -ptr.Offset, "Bad copy instruction detected.");
        
        _bytesRead += toRead;

        var loadIndex = (_lookaroundIndex + ptr.Offset) % _lookaround.Length;
        if (loadIndex < 0)
        {
            loadIndex += _lookaround.Length;
        }

        // Copy a run from the lookaround buffer into the output buffer.
        for (var i = 0; i < buffer.Length; i++)
        {
            // Read data from the lookaround buffer.
            buffer[i] = _lookaround[loadIndex];

            // Read through to the lookaround buffer.
            _lookaround[_lookaroundIndex++] = _lookaround[loadIndex++];
            loadIndex %= _lookaround.Length;
            _lookaroundIndex %= _lookaround.Length;
        }
            
        if (toRead != ptr.Size)
        {
            // Store the current PRS instruction so we can resume it on the next read.
            _currentInstruction = new PrsPointerState { LoadIndex = loadIndex, Size = ptr.Size, BytesRead = toRead };
        }

        Debug.Assert(_bytesRead % _lookaround.Length == _lookaroundIndex,
            "Bytes read and lookaround index are not synced.");
    }

    private void SeekPointer(PrsPointer ptr, int toSeek)
    {
        Debug.Assert(ptr.Offset != 0 && _bytesRead >= -ptr.Offset, "Bad copy instruction detected.");
        
        _bytesRead += toSeek;
                
        var loadIndex = (_lookaroundIndex + ptr.Offset) % _lookaround.Length;
        if (loadIndex < 0)
        {
            loadIndex += _lookaround.Length;
        }
            
        for (var index = 0; index < toSeek; index++)
        {
            _lookaround[_lookaroundIndex++] = _lookaround[loadIndex++];
            loadIndex %= _lookaround.Length;
            _lookaroundIndex %= _lookaround.Length;
        }

        if (toSeek != ptr.Size)
        {
            // Store the current PRS instruction so we can resume it on the next read.
            _currentInstruction = new PrsPointerState { LoadIndex = loadIndex, Size = ptr.Size, BytesRead = toSeek };
        }

        Debug.Assert(_bytesRead % _lookaround.Length == _lookaroundIndex,
            "Bytes read and lookaround index are not synced.");
    }

    /// <summary>
    /// Reads the next decompression instruction from the stream.
    /// </summary>
    private PrsInstruction GetNextInstruction()
    {
        if (GetControlBit())
        {
            return new PrsLiteral();
        }

        if (GetControlBit())
        {
            var data0 = GetNextByte();
            var data1 = GetNextByte();
            if (ReadEof(data0, data1))
            {
                return new PrsEof();
            }

            // Long search; long size or long search; short size
            var (offset, size) = ReadLongRun(data0, data1);
            return new PrsPointer(offset, size);
        }
        else
        {
            // Short search; short size
            var (offset, size) = ReadShortRun();
            return new PrsPointer(offset, size);
        }
    }

    /// <summary>
    /// Reads an EOF for the current decompression stream.
    /// </summary>
    /// <param name="data0">The (n - 2) byte relative to the current stream position.</param>
    /// <param name="data1">The (n - 1) byte relative to the current stream position.</param>
    /// <returns>Whether or not stream reading should terminate.</returns>
    private static bool ReadEof(int data0, int data1)
    {
        return data0 == 0 && data1 == 0;
    }

    /// <summary>
    /// Reads the instructions for a long search, long size or a long search, short size
    /// run copy.
    /// </summary>
    /// <param name="data0">The (n - 2) byte relative to the current stream position.</param>
    /// <param name="data1">The (n - 1) byte relative to the current stream position.</param>
    /// <returns>A tuple of (offset, size).</returns>
    private (int, int) ReadLongRun(int data0, int data1)
    {
        var controlOffset = (data1 << 5) + (data0 >> 3) - 8192;
        var controlSize = data0 & 0b00000111;

        if (controlSize == 0)
        {
            // Long search; long size
            controlSize = GetNextByte() + MinLongCopyLength;
        }
        else
        {
            // Long search; short size
            controlSize += 2;
        }

        return (controlOffset, controlSize);
    }

    /// <summary>
    /// Reads the instructions for a short search, short size run copy.
    /// </summary>
    /// <returns>A tuple of (offset, size).</returns>
    private (int, int) ReadShortRun()
    {
        var controlSize = 2;
        if (GetControlBit())
        {
            controlSize += 2;
        }
        
        if (GetControlBit())
        {
            controlSize++;
        }

        var controlOffset = GetNextByte() - 256;

        return (controlOffset, controlSize);
    }

    private bool GetControlBit()
    {
        if (_ctrlByteCounter == 8)
        {
            _ctrlByte = (byte)GetNextByte();
            _ctrlByteCounter = 0;
        }

        return (_ctrlByte & (1 << _ctrlByteCounter++)) > 0;
    }

    private int GetNextByte()
    {
        var initialPos = _stream.Position;
        var next = _stream.ReadByte();
        Debug.Assert(next != -1, "Unexpected end of stream.");
        Debug.Assert(_stream.Position == initialPos + 1, "Unexpected end of stream.");
        return next;
    }
}