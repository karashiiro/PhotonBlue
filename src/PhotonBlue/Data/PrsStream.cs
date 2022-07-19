using System.Diagnostics;

namespace PhotonBlue.Data;

public class PrsStream : Stream
{
    private const int MinLongCopyLength = 10;

    public override bool CanRead { get; }
    public override bool CanSeek { get; }
    public override bool CanWrite { get; }
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    private int _ctrlByteCounter = 0;
    private byte _ctrlByte = 0;

    private readonly Stream _stream;

    public PrsStream(Stream input)
    {
        _stream = input;

        CanRead = true;
        CanSeek = false;
        CanWrite = true;
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var outIndex = offset;
        var endIndex = offset + count;
        while (outIndex < endIndex)
        {
            while (GetControlBit())
            {
                // Raw data read
                buffer[outIndex++] = (byte)GetNextByte();
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

            Debug.Assert(controlOffset != 0 && outIndex >= Math.Abs(controlOffset), "Bad copy instruction detected.");

            var loadIndex = outIndex + controlOffset;
            for (var index = 0; index < controlSize; ++index)
            {
                buffer[outIndex++] = buffer[loadIndex++];
            }
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
        throw new NotSupportedException();
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