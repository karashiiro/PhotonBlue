using System.Diagnostics;

namespace PhotonBlue.Data;

public class PrsStream : Stream
{
    private class PrsPointer
    {
        public int LoadIndex { get; set; }
        public int BytesRead { get; set; }
        public int Size { get; init; }

        public int Read(IList<byte> lookaround, int lookaroundOffset, IList<byte> buffer, int bufferIndex, int bufferCount)
        {
            var initialIndex = bufferIndex;
            var endIndex = bufferIndex + bufferCount;
            for (; BytesRead < Size; ++BytesRead)
            {
                buffer[bufferIndex] = lookaround[LoadIndex++];
                
                // Read through to the lookaround buffer.
                lookaround[lookaroundOffset++] = buffer[bufferIndex++];
                LoadIndex %= lookaround.Count;
                lookaroundOffset %= lookaround.Count;
                
                if (bufferIndex == endIndex)
                {
                    ++BytesRead;
                    return bufferIndex - initialIndex;
                }
            }
            
            return bufferIndex - initialIndex;
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

    private int _ctrlByteCounter = 0;
    private byte _ctrlByte = 0;

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
            
            for (var index = 0; index < controlSize; ++index)
            {
                buffer[outIndex] = _lookaround[loadIndex++];
                
                // Read data into the lookaround, as well.
                _lookaround[_lookaroundIndex++] = buffer[outIndex++];
                loadIndex %= _lookaround.Length;
                _lookaroundIndex %= _lookaround.Length;
                _bytesRead++;

                if (outIndex == endIndex && index + 1 != controlSize)
                {
                    // Store the current PRS instruction so we can resume it on the next read.
                    _currentInstruction = new PrsPointer { LoadIndex = loadIndex, Size = controlSize, BytesRead = index + 1 };
                    break;
                }
            }
            
            Debug.Assert(_bytesRead % _lookaround.Length == _lookaroundIndex, "Bytes read and lookaround index are not synced.");
        }

        return outIndex - offset;
    }

    private int GetNextByte()
    {
        var next = _stream.ReadByte();
        Debug.Assert(next != -1, "Unexpected end of stream.");
        return next;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        // This is a poor implementation, but it correctly manages the lookaround buffer's state.
        var absoluteOffset = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => offset + _stream.Position,
            SeekOrigin.End => _stream.Length - offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null),
        };

        Debug.Assert(absoluteOffset >= _stream.Position, "This stream does not support seeking to consumed data.");

        var count = absoluteOffset - _stream.Position;
        var buf = new byte[count];
        return _stream.Position + Read(buf, 0, Convert.ToInt32(count));
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
        if (_ctrlByteCounter == 0)
        {
            var next = _stream.ReadByte();
            Debug.Assert(next != -1, "Unexpected end of stream.");
            
            _ctrlByte = (byte)next;
            _ctrlByteCounter = 8;
        }

        var flag = (_ctrlByte & 1U) > 0U;
        _ctrlByte >>= 1;
        --_ctrlByteCounter;

        return flag;
    }
}