namespace PhotonBlue.Data.Files;

public class IcePrsInputStream : Stream
{
    public override bool CanRead => _stream.CanRead;
    public override bool CanSeek => _stream.CanSeek;
    public override bool CanWrite => _stream.CanWrite;
    
    public override long Length => _length;
    public override long Position
    {
        get => _position;
        set
        {
            _stream.Position = value;
            _position = value;
        }
    }

    private readonly Stream _stream;

    private long _length;
    private long _position;
    
    public IcePrsInputStream(Stream data)
    {
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
        
        var nRead = _stream.Read(buffer, offset, count);
        for (var i = offset; i < offset + count; i++)
        {
            buffer[i] ^= 149;
        }
        
        _position += nRead;
        return nRead;
    }

    public override int ReadByte()
    {
        var next = _stream.ReadByte();
        _position++;
        return next == -1 ? next : next ^ 149;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        _position = _stream.Seek(offset, origin);
        return _position;
    }

    public override void SetLength(long value)
    {
        _stream.SetLength(value);
        _length = value;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }
}