namespace PhotonBlue.PRS;

public ref struct InclusiveRangeSet
{
    private readonly Span<int> _ranges;
    private int _valuesCount;

    public int Count
    {
        get => _valuesCount / 2;
        set => _valuesCount = value * 2;
    }

    public InclusiveRangeSet(Span<int> ranges, int initialCount)
    {
        _ranges = ranges;
        _valuesCount = initialCount;
    }

    public void Add(int start, int end)
    {
        if (Contains(start, end))
        {
            return;
        }

        if (ContainsLeft(start))
        {
            // New range is left-aligned with an existing range
            _ranges[_valuesCount++] = end;
            _ranges[_valuesCount++] = end + 1;
        }
        else if (ContainsRight(end))
        {
            // New range is right-aligned with an existing range
            _ranges[_valuesCount++] = start;
            _ranges[_valuesCount++] = start - 1;
        }
        else
        {
            _ranges[_valuesCount++] = start;
            _ranges[_valuesCount++] = end;
        }

        _ranges[.._valuesCount].Sort();
    }

    private bool ContainsLeft(int value)
    {
        for (var i = 0; i < _valuesCount; i += 2)
        {
            if (_ranges[i] == value)
            {
                return true;
            }
        }

        return false;
    }
    
    private bool ContainsRight(int value)
    {
        for (var i = 1; i < _valuesCount; i += 2)
        {
            if (_ranges[i] == value)
            {
                return true;
            }
        }

        return false;
    }

    public bool Contains(int start, int end)
    {
        for (var i = 0; i < Count; i++)
        {
            var r0 = _ranges[i * 2];
            var r1 = _ranges[i * 2 + 1];
            if (r0 == start && r1 == end)
            {
                return true;
            }
        }

        return false;
    }

    public void Sort()
    {
        _ranges[.._valuesCount].Sort();
    }

    public static void Modulus(Span<int> values, int mod)
    {
        for (var i = 0; i < values.Length; i++)
        {
            values[i] %= mod;
        }
    }
}