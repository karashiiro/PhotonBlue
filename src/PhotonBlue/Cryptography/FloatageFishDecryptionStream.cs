namespace PhotonBlue.Cryptography;

internal sealed class FloatageFishDecryptionStream : Stream
{
    public override bool CanRead => _stream.CanRead;
    public override bool CanSeek => _stream.CanSeek;
    public override bool CanWrite => false;
    
    // ReSharper disable once ConvertToAutoPropertyWithPrivateSetter
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
    private readonly byte _key;

    private long _length;
    private long _position;

    public FloatageFishDecryptionStream(Stream data, uint blowfishKey, int shift)
    {
        _stream = data;
        _key = FloatageFish.CalculateKey(blowfishKey, shift);

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
        FloatageFish.DecryptBlockWithKey(buffer, offset, nRead, _key);
        _position += nRead;
        return nRead;
    }
    
    public override int ReadByte()
    {
        var next = _stream.ReadByte();
        _position++;
        return next == -1 ? next : FloatageFish.DecryptByteWithKey((byte)next, _key);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        _position += _stream.Seek(offset, origin);
        return _position;
    }

    public override void SetLength(long value)
    {
        _stream.SetLength(value);
        _length = value;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }
}