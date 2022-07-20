using System.Diagnostics;

namespace PhotonBlue;

public class PartitionedStream : Stream
{
    public override bool CanRead => _stream.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => PartitionLength;

    public override long Position
    {
        get => PartitionPosition;
        set => _stream.Position = Math.Max(0, Math.Min(PartitionStart + PartitionLength, value + PartitionStart));
    }

    private long PartitionStart { get; set; }

    private long PartitionLength => _partitions[_currentPartition];
    private long PartitionPosition => _stream.Position - PartitionStart;

    private readonly Stream _stream;
    private readonly IReadOnlyList<long> _partitions;
    private int _currentPartition;

    public PartitionedStream(Stream data, IReadOnlyList<long> partitions)
    {
        _stream = data;
        _partitions = partitions;
        _currentPartition = 0;
        
        PartitionStart = 0;
    }

    public void NextPartition()
    {
        if (_currentPartition == _partitions.Count)
        {
            throw new InvalidOperationException("The final partition is already selected.");
        }

        _currentPartition++;
        PartitionStart = _partitions.Take(_currentPartition).Sum();
        
        // Seek to the start of the partition, in case we aren't there yet
        _stream.Seek(PartitionStart - _stream.Position, SeekOrigin.Current);
        Debug.Assert(_stream.Position == PartitionStart);
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
        var absoluteOffset = origin switch
        {
            SeekOrigin.Begin => offset + PartitionStart,
            SeekOrigin.Current => offset + _stream.Position,
            SeekOrigin.End => offset + PartitionLength + PartitionStart,
            _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null),
        };

        var constrainedOffset = Math.Max(0, Math.Min(PartitionStart + PartitionLength, absoluteOffset));
        return _stream.Seek(constrainedOffset, origin) - PartitionStart;
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