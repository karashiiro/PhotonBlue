namespace PhotonBlue.Cryptography;

public class FloatageFishDecryptionStream : Stream
{
    public override bool CanRead => _stream.CanRead;
    public override bool CanSeek => _stream.CanSeek;
    public override bool CanWrite => false;
    public override long Length => _stream.Length;

    public override long Position
    {
        get => _stream.Position;
        set => _stream.Position = value;
    }

    private readonly Stream _stream;
    private readonly uint _key;
    private readonly int _shift;

    public FloatageFishDecryptionStream(Stream data, uint key, int shift)
    {
        _stream = data;
        _key = key;
        _shift = shift;
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
        var nRead = _stream.Read(buffer, outIndex, count);
        FloatageFish.DecryptBlock(buffer, Convert.ToUInt32(outIndex), Convert.ToUInt32(nRead), _key, _shift);
        outIndex += nRead;
        return outIndex - offset;
    }
    
    public override int ReadByte()
    {
        return FloatageFish.DecryptByte((byte)_stream.ReadByte(), _key, 16);
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
        throw new NotSupportedException();
    }
}