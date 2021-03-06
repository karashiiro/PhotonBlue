using System.Buffers;
using System.Diagnostics;

namespace PhotonBlue.Data;

// Is this actually a variant of LZSS, and not LZ77? The implementation looks
// much more similar to that than to LZ77, but with some small tweaks to how
// EOF, offset, and size are calculated. LZSS is itself a variant of LZ77, of
// course, but it's more useful to be specific.
public class PrsStream : Stream
{
    private class PrsPointerState
    {
        public int LoadIndex { get; set; }
        public int BytesRead { get; set; }
        public int Size { get; init; }

        public int Read(Span<byte> lookaround, int lookaroundOffset, Span<byte> buffer)
        {
            var toRead = Math.Min(buffer.Length, Size - BytesRead);
            BytesRead += toRead;
            
            var copyTarget = buffer[..toRead];
            for (var i = 0; i < copyTarget.Length; i++)
            {
                // Read data from the lookaround buffer.
                copyTarget[i] = lookaround[LoadIndex];

                // Read through to the lookaround buffer.
                lookaround[lookaroundOffset++] = lookaround[LoadIndex++];

                // Doing these modulus assignments every loop is much slower
                // than doing comparisons every loop instead.
                if (LoadIndex == lookaround.Length)
                {
                    LoadIndex %= lookaround.Length;
                }

                if (lookaroundOffset == lookaround.Length)
                {
                    lookaroundOffset %= lookaround.Length;
                }
            }
            
            return toRead;
        }

        public int Skip(Span<byte> lookaround, int lookaroundOffset, int count)
        {
            var toRead = Math.Min(count, Size - BytesRead);
            BytesRead += toRead;
            for (var i = 0; i < toRead; i++)
            {
                lookaround[lookaroundOffset++] = lookaround[LoadIndex++];

                if (LoadIndex == lookaround.Length)
                {
                    LoadIndex %= lookaround.Length;
                }

                if (lookaroundOffset == lookaround.Length)
                {
                    lookaroundOffset %= lookaround.Length;
                }
            }

            return toRead;
        }
    }

    private enum PrsInstruction
    {
        Eof,
        Literal,
        Pointer,
    }
    
    private struct ControlByte
    {
        public byte Data;

        public ControlByte(byte data)
        {
            Data = data;
        }

        public readonly bool GetBit(int offset)
        {
            return (Data & (1 << offset)) > 0;
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

    private ControlByte _ctrlByte;
    private int _ctrlByteCounter;

    // An internal buffer for maintaining some of the decompressed data
    // between reads. This is used as a ring queue.
    private readonly ArraySegment<byte> _lookaround;
    private readonly byte[] _lookaroundRaw;
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
        _ctrlByte = new ControlByte(0);

        _lookaroundRaw = ArrayPool<byte>.Shared.Rent(0x1FFF);
        _lookaround = new ArraySegment<byte>(_lookaroundRaw, 0, 0x1FFF);
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
            var nRead = _currentInstruction.Read(_lookaround, _lookaroundIndex, buffer.AsSpan(outIndex, count));
            outIndex += nRead;
            _bytesRead += nRead;

            _lookaroundIndex += nRead;
            if (_lookaroundIndex > _lookaround.Count)
            {
                _lookaroundIndex %= _lookaround.Count;
            }

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
                case (PrsInstruction.Eof, _, _):
                    return outIndex - offset;
                case (PrsInstruction.Literal, _, _):
                    // Raw data read
                    buffer[outIndex] = (byte)GetNextByte();

                    // Every byte that is read to the output buffer also needs to be read into
                    // the lookaround. This allows for incremental decompression, which is important
                    // for minimizing the amount of work done during file indexing.
                    _lookaround[_lookaroundIndex++] = buffer[outIndex++];
                    _bytesRead++;

                    if (_lookaroundIndex == _lookaround.Count)
                    {
                        _lookaroundIndex %= _lookaround.Count;
                    }

                    break;
                case (PrsInstruction.Pointer, _, _) ptr:
                {
                    var (_, prsOffset, prsSize) = ptr;
                    var toRead = Math.Min(prsSize, endIndex - outIndex);
                    ReadPointer(prsOffset, prsSize, buffer.AsSpan(outIndex, toRead), toRead);
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
            _bytesRead += nRead;

            _lookaroundIndex += nRead;
            if (_lookaroundIndex > _lookaround.Count)
            {
                _lookaroundIndex %= _lookaround.Count;
            }

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
                case (PrsInstruction.Eof, _, _):
                    return outIndex - offset;
                case (PrsInstruction.Literal, _, _):
                    // Data can be carried from the very beginning of the stream, all the way
                    // to the end of the decompressed data, so it's difficult to determine when
                    // a skip can or can't be done.
                    _lookaround[_lookaroundIndex++] = (byte)GetNextByte();
                    _bytesRead++;
                    outIndex++;

                    if (_lookaroundIndex == _lookaround.Count)
                    {
                        _lookaroundIndex %= _lookaround.Count;
                    }

                    break;
                case (PrsInstruction.Pointer, _, _) ptr:
                {
                    var (_, prsOffset, prsSize) = ptr;
                    var toSeek = Math.Min(prsSize, count - outIndex);
                    SeekPointer(prsOffset, prsSize, toSeek);
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

    /// <summary>
    /// Executes a pointer instruction, reading data to an output buffer.
    /// </summary>
    /// <param name="offset">The offset to copy data from.</param>
    /// <param name="size">The length of the block to copy.</param>
    /// <param name="buffer">The buffer to copy data to.</param>
    /// <param name="toRead">
    /// The maximum number of bytes to process. Any further bytes will be processed the next time data
    /// is read or seeked.
    /// </param>
    private void ReadPointer(int offset, int size, Span<byte> buffer, int toRead)
    {
        Debug.Assert(offset != 0 && _bytesRead >= -offset, "Bad copy instruction detected.");

        _bytesRead += toRead;

        var loadIndex = _lookaroundIndex + offset;
        if (loadIndex < 0)
        {
            loadIndex += _lookaround.Count;
        }

        if (loadIndex > _lookaround.Count)
        {
            loadIndex %= _lookaround.Count;
        }

        // Copy a run from the lookaround buffer into the output buffer.
        if (CanFastCopy(_lookaroundIndex, _lookaround.Count, loadIndex, toRead))
        {
            // If the source region and the destination region don't overlap, and neither region
            // loops around to the start of the array, we can just do a simple array copy, which
            // is much faster than executing the for loop.
            var copySrc = _lookaround.AsSpan(loadIndex, toRead);
            copySrc.CopyTo(buffer);
            copySrc.CopyTo(_lookaround.AsSpan(_lookaroundIndex, toRead));
            _lookaroundIndex += toRead;
            loadIndex += toRead;
        }
        else
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                // Read data from the lookaround buffer.
                buffer[i] = _lookaround[loadIndex];

                // Read through to the lookaround buffer.
                _lookaround[_lookaroundIndex++] = _lookaround[loadIndex++];

                // Doing these modulus assignments every loop is much slower
                // than doing comparisons every loop instead.
                if (loadIndex == _lookaround.Count)
                {
                    loadIndex %= _lookaround.Count;
                }

                if (_lookaroundIndex == _lookaround.Count)
                {
                    _lookaroundIndex %= _lookaround.Count;
                }
            }
        }

        if (toRead != size)
        {
            // Store the current PRS instruction so we can resume it on the next read.
            _currentInstruction = new PrsPointerState { LoadIndex = loadIndex, Size = size, BytesRead = toRead };
        }

        Debug.Assert(_bytesRead % _lookaround.Count == _lookaroundIndex,
            "Bytes read and lookaround index are not synced.");
    }

    /// <summary>
    /// Follows a pointer instruction without reading data to an output buffer. This still
    /// copies data within the lookaround buffer.
    /// </summary>
    /// <param name="offset">The offset to copy data from.</param>
    /// <param name="size">The length of the block to copy.</param>
    /// <param name="toSeek">
    /// The maximum number of bytes to process. Any further bytes will be processed the next time data
    /// is read or seeked.
    /// </param>
    private void SeekPointer(int offset, int size, int toSeek)
    {
        Debug.Assert(offset != 0 && _bytesRead >= -offset, "Bad copy instruction detected.");

        _bytesRead += toSeek;

        var loadIndex = _lookaroundIndex + offset;
        if (loadIndex < 0)
        {
            loadIndex += _lookaround.Count;
        }

        if (loadIndex > _lookaround.Count)
        {
            loadIndex %= _lookaround.Count;
        }

        if (CanFastCopy(_lookaroundIndex, _lookaround.Count, loadIndex, toSeek))
        {
            var copySrc = _lookaround.AsSpan(loadIndex, toSeek);
            copySrc.CopyTo(_lookaround.AsSpan(_lookaroundIndex, toSeek));
            _lookaroundIndex += toSeek;
            loadIndex += toSeek;
        }
        else
        {
            for (var index = 0; index < toSeek; index++)
            {
                _lookaround[_lookaroundIndex++] = _lookaround[loadIndex++];

                if (loadIndex == _lookaround.Count)
                {
                    loadIndex %= _lookaround.Count;
                }

                if (_lookaroundIndex == _lookaround.Count)
                {
                    _lookaroundIndex %= _lookaround.Count;
                }
            }
        }

        if (toSeek != size)
        {
            // Store the current PRS instruction so we can resume it on the next read.
            _currentInstruction = new PrsPointerState { LoadIndex = loadIndex, Size = size, BytesRead = toSeek };
        }

        Debug.Assert(_bytesRead % _lookaround.Count == _lookaroundIndex,
            "Bytes read and lookaround index are not synced.");
    }

    /// <summary>
    /// Returns whether or not it is safe to perform a contiguous copy within the
    /// lookaround buffer at the provided index.
    /// </summary>
    /// <param name="lookaroundIndex">The lookaround buffer index.</param>
    /// <param name="lookaroundLength">The length of the lookaround buffer.</param>
    /// <param name="loadIndex">The source index to copy from.</param>
    /// <param name="size">The length of the data to be copied.</param>
    private static bool CanFastCopy(int lookaroundIndex, int lookaroundLength, int loadIndex, int size)
    {
        return Math.Max(lookaroundIndex, loadIndex) + size < lookaroundLength &&
            Math.Abs(lookaroundIndex - loadIndex) > size;
    }

    /// <summary>
    /// Reads the next decompression instruction from the stream.
    /// </summary>
    /// <returns>
    /// A tuple of (instruction, offset, size), where the instruction is described
    /// in <see cref="PrsInstruction"/>. The offset and size are 0 if the instruction
    /// isn't a pointer.
    /// </returns>
    private (PrsInstruction, int, int) GetNextInstruction()
    {
        if (GetControlBit())
        {
            return (PrsInstruction.Literal, 0, 0);
        }

        if (GetControlBit())
        {
            var data0 = GetNextByte();
            var data1 = GetNextByte();
            if (ReadEof(data0, data1))
            {
                return (PrsInstruction.Eof, 0, 0);
            }

            // Long search; long size or long search; short size
            var (offset, size) = ReadLongRun(data0, data1);
            return (PrsInstruction.Pointer, offset, size);
        }
        else
        {
            // Short search; short size
            var (offset, size) = ReadShortRun();
            return (PrsInstruction.Pointer, offset, size);
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

    /// <summary>
    /// Reads the next control bit from the stream.
    /// </summary>
    /// <returns>The control bit value.</returns>
    private bool GetControlBit()
    {
        if (_ctrlByteCounter == 8)
        {
            _ctrlByte.Data = (byte)GetNextByte();
            _ctrlByteCounter = 0;
        }

        return _ctrlByte.GetBit(_ctrlByteCounter++);
    }

    /// <summary>
    /// Returns the next byte from the stream. This is jitted out in Release mode.
    /// </summary>
    private int GetNextByte()
    {
        var next = _stream.ReadByte();
        Debug.Assert(next != -1, "Unexpected end of stream.");
        return next;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ArrayPool<byte>.Shared.Return(_lookaroundRaw);
        }
        
        base.Dispose(disposing);
    }
}