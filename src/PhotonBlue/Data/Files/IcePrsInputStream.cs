namespace PhotonBlue.Data.Files;

public class IcePrsInputStream : Stream
{
    public override bool CanRead => _stream.CanRead;
    public override bool CanSeek => _stream.CanSeek;
    public override bool CanWrite => _stream.CanWrite;
    public override long Length => _stream.Length;
    public override long Position { get => _stream.Position; set => _stream.Position = value; }

    private readonly Stream _stream;
    
    public IcePrsInputStream(Stream data)
    {
        _stream = data;
    }
    
    public override void Flush()
    {
        _stream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var n = _stream.Read(buffer, offset, count);
        for (var i = offset; i < offset + count; i++)
        {
            buffer[i] ^= 149;
        }
        
        return n;
    }

    public override int ReadByte()
    {
        return _stream.ReadByte() ^ 149;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return _stream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        _stream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }
}