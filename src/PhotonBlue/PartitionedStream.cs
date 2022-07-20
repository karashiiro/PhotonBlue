namespace PhotonBlue;

public class PartitionedStream : Stream
{
    public override bool CanRead => _stream.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => PartitionLength;
    public override long Position { get => PartitionPosition; set => _stream.Position = value + PartitionStart; }

    private long BytesAfterPartition => _partitions.Skip(_currentPartition + 1).Sum();
    private long PartitionLength => _partitions[_currentPartition];
    private long PartitionStart => _partitions.Take(_currentPartition).Sum();
    private long PartitionPosition => _stream.Position - PartitionStart;

    private readonly Stream _stream;
    private readonly IReadOnlyList<long> _partitions;
    private int _currentPartition;

    public PartitionedStream(Stream data, IReadOnlyList<long> partitions)
    {
        _stream = data;
        _partitions = partitions;
        _currentPartition = 0;
    }

    public void NextPartition()
    {
        if (_currentPartition == _partitions.Count)
        {
            throw new InvalidOperationException("The final partition is already selected.");
        }

        _currentPartition++;
    }
    
    public override void Flush()
    {
        throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var toRead = Math.Min(count, PartitionLength - PartitionPosition);
        return _stream.Read(buffer, offset, Convert.ToInt32(toRead));
    }

    public override int ReadByte()
    {
        return _stream.ReadByte();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var actualOffset = origin switch
        {
            SeekOrigin.Begin => offset + PartitionStart,
            SeekOrigin.Current => offset,
            SeekOrigin.End => offset + BytesAfterPartition,
            _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null),
        };

        return _stream.Seek(actualOffset, origin);
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }
}