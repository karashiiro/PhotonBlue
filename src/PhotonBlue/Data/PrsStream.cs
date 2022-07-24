using System.Diagnostics;

namespace PhotonBlue.Data;

public class PrsStream : Stream
{
    private class PrsPointer
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
    private PrsPointer? _currentInstruction;

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
            if (GetControlBit())
            {
                // Raw data read
                buffer[outIndex] = (byte)GetNextByte();

                // Every byte that is read to the output buffer also needs to be read into
                // the lookaround. This allows for incremental decompression, which is important
                // for minimizing the amount of work done during file indexing.
                _lookaround[_lookaroundIndex++] = buffer[outIndex++];
                _lookaroundIndex %= _lookaround.Length;
                _bytesRead++;

                continue;
            }

            int controlOffset;
            int controlSize;
            if (GetControlBit())
            {
                var data0 = GetNextByte();
                var data1 = GetNextByte();
                if (data0 == 0 && data1 == 0)
                {
                    // EOF
                    break;
                }

                controlOffset = (data1 << 5) + (data0 >> 3) - 8192;
                controlSize = data0 & 0b00000111;

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
            }
            else
            {
                // Short search; short size
                controlSize = 2;
                if (GetControlBit())
                    controlSize += 2;
                if (GetControlBit())
                    ++controlSize;

                controlOffset = GetNextByte() - 256;
            }

            Debug.Assert(controlOffset != 0 && _bytesRead >= -controlOffset, "Bad copy instruction detected.");
            
            var loadIndex = (_lookaroundIndex + controlOffset) % _lookaround.Length;
            if (loadIndex < 0)
            {
                loadIndex += _lookaround.Length;
            }

            // Copy a run from the lookaround buffer into the output buffer.
            var toRead = Math.Min(controlSize, endIndex - outIndex);
            var copyTarget = buffer.AsSpan(outIndex, toRead);
            for (var i = 0; i < copyTarget.Length; i++)
            {
                // Read data from the lookaround buffer.
                copyTarget[i] = _lookaround[loadIndex];

                // Read through to the lookaround buffer.
                _lookaround[_lookaroundIndex++] = _lookaround[loadIndex++];
                loadIndex %= _lookaround.Length;
                _lookaroundIndex %= _lookaround.Length;
            }
            
            outIndex += toRead;
            _bytesRead += toRead;

            if (toRead != controlSize)
            {
                // Store the current PRS instruction so we can resume it on the next read.
                _currentInstruction = new PrsPointer { LoadIndex = loadIndex, Size = controlSize, BytesRead = toRead };
            }

            Debug.Assert(_bytesRead % _lookaround.Length == _lookaroundIndex,
                "Bytes read and lookaround index are not synced.");
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
            if (GetControlBit())
            {
                _lookaround[_lookaroundIndex++] = (byte)GetNextByte();
                _lookaroundIndex %= _lookaround.Length;
                _bytesRead++;
                outIndex++;
                continue;
            }

            int controlOffset;
            int controlSize;
            if (GetControlBit())
            {
                var data0 = GetNextByte();
                var data1 = GetNextByte();
                if (data0 == 0 && data1 == 0)
                {
                    // EOF
                    break;
                }

                controlOffset = (data1 << 5) + (data0 >> 3) - 8192;
                controlSize = data0 & 0b00000111;

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
            }
            else
            {
                // Short search; short size
                controlSize = 2;
                if (GetControlBit())
                    controlSize += 2;
                if (GetControlBit())
                    ++controlSize;

                controlOffset = GetNextByte() - 256;
            }

            Debug.Assert(controlOffset != 0 && _bytesRead >= -controlOffset, "Bad copy instruction detected.");

            var loadIndex = (_lookaroundIndex + controlOffset) % _lookaround.Length;
            if (loadIndex < 0)
            {
                loadIndex += _lookaround.Length;
            }

            var toRead = Math.Min(controlSize, count - outIndex);
            _bytesRead += toRead;
            outIndex += toRead;
            
            for (var index = 0; index < toRead; index++)
            {
                _lookaround[_lookaroundIndex++] = _lookaround[loadIndex++];
                loadIndex %= _lookaround.Length;
                _lookaroundIndex %= _lookaround.Length;
            }

            if (toRead != controlSize)
            {
                // Store the current PRS instruction so we can resume it on the next read.
                _currentInstruction = new PrsPointer { LoadIndex = loadIndex, Size = controlSize, BytesRead = toRead };
            }

            Debug.Assert(_bytesRead % _lookaround.Length == _lookaroundIndex,
                "Bytes read and lookaround index are not synced.");
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