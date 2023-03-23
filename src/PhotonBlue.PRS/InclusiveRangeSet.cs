namespace PhotonBlue.PRS;

internal ref struct InclusiveRangeSet
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
        var containsStart = ContainsLeft(start);
        var containsEnd = ContainsRight(end);

        // ReSharper disable once ConvertIfStatementToSwitchStatement
        if (containsStart && containsEnd)
        {
            return;
        }

        if (containsStart)
        {
            // New range is left-aligned with an existing range
            _ranges[_valuesCount++] = end;
            _ranges[_valuesCount++] = end + 1;
        }
        else if (containsEnd)
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

        InsertionSort(_ranges[.._valuesCount]);
    }

    private static void InsertionSort(Span<int> keys)
    {
        // Copied right out of the Span<T>.Sort() implementation, with some
        // minor optimizations.
        for (var i = 0; i < keys.Length - 1; i++)
        {
            var t = keys[i + 1];

            var j = i;
            while (j >= 0 && t < keys[j])
            {
                keys[j + 1] = keys[j];
                j--;
            }

            keys[j + 1] = t;
        }
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
}