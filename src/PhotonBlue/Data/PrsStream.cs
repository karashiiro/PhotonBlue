namespace PhotonBlue.Data;

public class PrsStream : Stream
{
    public override bool CanRead { get; }
    public override bool CanSeek { get; }
    public override bool CanWrite { get; }
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }
    
    private int _ctrlByteCounter = 1;
    private byte _origCtrlByte = 0;
    private byte _ctrlByte = 0;
    private int _currDecompPos = 0;

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
        var initialPos = _stream.Position;
        
        // These variable names might be incorrect; I inferred these based on the LZ77
        // algorithm description.
        var outIndex = offset;
        while (outIndex < count && _stream.Position < _stream.Length)
        {
            while (GetControlBit())
                buffer[outIndex++] = (byte)_stream.ReadByte();
            
            int lookahead;
            int blockEndIndex;
            if (GetControlBit())
            {
                if (_stream.Position < _stream.Length)
                {
                    var unk0 = _stream.ReadByte();
                    var unk1 = _stream.ReadByte();
                    if (unk0 != 0 || unk1 != 0)
                    {
                        lookahead = (unk1 << 5) + (unk0 >> 3) - 8192;
                        var unk2 = unk0 & 7;
                        blockEndIndex = unk2 != 0 ? unk2 + 2 : _stream.ReadByte() + 10;
                    }
                    else
                        break;
                }
                else
                    break;
            }
            else
            {
                blockEndIndex = 2;
                if (GetControlBit())
                    blockEndIndex += 2;
                if (GetControlBit())
                    ++blockEndIndex;
                lookahead = _stream.ReadByte() - 256;
            }
            
            var lookaheadIndex = lookahead + outIndex;
            for (var index = 0; index < blockEndIndex && outIndex < count; ++index)
                buffer[outIndex++] = buffer[lookaheadIndex++];
        }

        return Convert.ToInt32(_stream.Position - initialPos);
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
        --_ctrlByteCounter;
        
        if (_ctrlByteCounter == 0)
        {
            _origCtrlByte = (byte)_stream.ReadByte();
            _ctrlByte = _origCtrlByte;
            _ctrlByteCounter = 8;
        }
        
        var flag = (_ctrlByte & 1U) > 0U;
        _ctrlByte >>= 1;
        
        return flag;
    }
}