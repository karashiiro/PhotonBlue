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
    private byte _origCtrlByte = 0;
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
        // These variable names might be incorrect; I inferred these based on the LZ77
        // algorithm description and other PRS implementations.
        var outIndex = offset;
        var endIndex = offset + count;
        while (outIndex < endIndex)
        {
            while (GetControlBit())
            {
                // Raw data read
                buffer[outIndex++] = (byte)_stream.ReadByte();
            }

            int controlOffset;
            int controlSize;
            if (GetControlBit())
            {
                var data0 = _stream.ReadByte();
                var data1 = _stream.ReadByte();
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
                    controlSize = _stream.ReadByte() + MinLongCopyLength;
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

                controlOffset = _stream.ReadByte() - 256;
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
            _origCtrlByte = (byte)_stream.ReadByte();
            _ctrlByte = _origCtrlByte;
            _ctrlByteCounter = 8;
        }

        var flag = (_ctrlByte & 1U) > 0U;
        _ctrlByte >>= 1;
        --_ctrlByteCounter;

        return flag;
    }
}