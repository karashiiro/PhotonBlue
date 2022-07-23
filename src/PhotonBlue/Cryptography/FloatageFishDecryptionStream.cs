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
    private readonly byte _key;

    public FloatageFishDecryptionStream(Stream data, uint blowfishKey, int shift)
    {
        _stream = data;
        _key = FloatageFish.CalculateKey(blowfishKey, shift);
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
        FloatageFish.DecryptBlockWithKey(buffer, outIndex, nRead, _key);
        outIndex += nRead;
        return outIndex - offset;
    }
    
    public override int ReadByte()
    {
        var next = _stream.ReadByte();
        return next == -1 ? next : FloatageFish.DecryptByteWithKey((byte)next, _key);
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