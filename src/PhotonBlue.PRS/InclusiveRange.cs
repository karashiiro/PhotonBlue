namespace PhotonBlue.PRS;

public static class InclusiveRange
{
    public static int Length(Span<int> range)
    {
        return range[1] - range[0] + 1;
    }

    public static bool Contains(Span<int> range, int value)
    {
        return value >= range[0] && value <= range[1];
    }

    public static bool Intersects(Span<int> first, Span<int> second)
    {
        return Contains(first, second[0]) || Contains(first, second[1]);
    }

    public static void Intersection(Span<int> first, Span<int> second, Span<int> result)
    {
        if (Intersects(first, second))
        {
            result[0] = Math.Max(first[0], second[0]);
            result[1] = Math.Min(first[1], second[1]);
        }
        else
        {
            result[0] = -1;
            result[1] = -1;
        }
    }
}