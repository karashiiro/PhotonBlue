using System.Diagnostics;

namespace PhotonBlue;

public class PartitionedStream : Stream
{
    public override bool CanRead => _stream.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _partitionLength;

    public override long Position
    {
        get => _partitionPosition;
        set => _stream.Position = Math.Max(0, Math.Min(_partitionStart + _partitionLength, value + _partitionStart));
    }

    private long _partitionStart;
    private long _partitionAfter;
    private long _partitionLength;
    private long _partitionPosition;

    private readonly Stream _stream;
    private readonly IReadOnlyList<long> _partitions;
    private int _currentPartition;

    public PartitionedStream(Stream data, IReadOnlyList<long> partitions)
    {
        _stream = data;
        _partitions = partitions;
        _currentPartition = 0;
        
        _partitionStart = 0;
        _partitionLength = _partitions[_currentPartition];
        _partitionAfter = data.Length - _partitionLength;
        _partitionPosition = Math.Min(data.Position, _partitionLength);
    }

    public void NextPartition()
    {
        if (_currentPartition == _partitions.Count)
        {
            throw new InvalidOperationException("The final partition is already selected.");
        }

        _currentPartition++;
        _partitionStart = _partitions.Take(_currentPartition).Sum();
        _partitionLength = _partitions[_currentPartition];
        _partitionAfter = _stream.Length - _partitionLength;
        _partitionPosition = 0;
        
        // Seek to the start of the partition, in case we aren't there yet
        _stream.Seek(_partitionStart - _stream.Position, SeekOrigin.Current);
        Debug.Assert(_stream.Position == _partitionStart);
    }
    
    public override void Flush()
    {
        throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var toRead = Math.Min(count, _partitionLength - _partitionPosition);
        var nRead = _stream.Read(buffer, offset, Convert.ToInt32(toRead));
        _partitionPosition += nRead;
        return nRead;
    }

    public override int ReadByte()
    {
        if (_partitionPosition == _partitionLength)
        {
            return -1;
        }
        
        _partitionPosition++;
        return _stream.ReadByte();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var normalizedOffset = origin switch
        {
            SeekOrigin.Begin => offset + _partitionStart,
            SeekOrigin.Current => offset,
            SeekOrigin.End => _partitionAfter + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null),
        };

        _partitionPosition = _stream.Seek(normalizedOffset, origin) - _partitionStart;
        Debug.Assert(_partitionPosition <= _partitionLength, "Seeked past the end of the partition.");
        Debug.Assert(_partitionPosition >= 0, "Seeked before the beginning of the partition.");
        return _partitionPosition;
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