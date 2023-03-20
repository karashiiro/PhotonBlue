using System.Diagnostics;

namespace PhotonBlue.PRS;

public class InclusiveRangeUtils
{
    /// <summary>
    /// TODO: Add algorithm explanation
    /// </summary>
    /// <param name="source"></param>
    /// <param name="destination"></param>
    /// <param name="cut"></param>
    /// <returns></returns>
    public static int AlignRangesOverCut(
        Span<int> source,
        Span<int> destination,
        int cut)
    {
        // We can have at most four ranges (eight ints) for each part
        // TODO: Deduplicate cut indexes to use only three ranges
        Debug.Assert(source.Length == 8);
        Debug.Assert(destination.Length == 8);
        Debug.Assert(InclusiveRange.Length(source[..2]) == InclusiveRange.Length(destination[..2]));
        var sourceCuts = new InclusiveRangeSet(source, 2);
        var destinationCuts = new InclusiveRangeSet(destination, 2);

        // Save the original bounds for validation later
        Span<int> sourceBounds = stackalloc int[2];
        Span<int> destinationBounds = stackalloc int[2];
        source[..2].CopyTo(sourceBounds);
        destination[..2].CopyTo(destinationBounds);

        Span<int> scratch = stackalloc int[2];

        // Check if either range crosses the cut, and apply the cut if needed
        if (InclusiveRange.Contains(source[..2], cut))
        {
            source[2] = cut - 1;
            source[3] = cut;
            sourceCuts.Count++;
            sourceCuts.Sort();
        }

        if (InclusiveRange.Contains(destination[..2], cut))
        {
            destination[2] = cut - 1;
            destination[3] = cut;
            destinationCuts.Count++;
            destinationCuts.Sort();
        }

        // If the ranges intersect and were not already cut, cut them at the intersection points
        if (sourceCuts.Count == 1 &&
            destinationCuts.Count == 1 &&
            InclusiveRange.Intersects(source[..2], destination[..2]))
        {
            InclusiveRange.Intersection(source[..2], destination[..2], scratch);
            sourceCuts.Add(scratch[0], scratch[1]);
            destinationCuts.Add(scratch[0], scratch[1]);
        }

        // Align the two sets
        var sourceOffset = source[0];
        sourceBounds[0] -= sourceOffset;
        sourceBounds[1] -= sourceOffset;
        for (var i = 0; i < sourceCuts.Count * 2; i++)
        {
            source[i] -= sourceOffset;
        }

        var destinationOffset = destination[0];
        destinationBounds[0] -= destinationOffset;
        destinationBounds[1] -= destinationOffset;
        for (var i = 0; i < destinationCuts.Count * 2; i++)
        {
            destination[i] -= destinationOffset;
        }

        // Make cuts at the intersections between ranges
        {
            var sourceCount = sourceCuts.Count;
            var destinationCount = destinationCuts.Count;
            var count = Math.Max(sourceCuts.Count, destinationCuts.Count);
            for (var i = 0; i < count; i++)
            {
                var sr = source.Slice(Math.Clamp(i, 0, sourceCount - 1) * 2, 2);
                var dr = destination.Slice(Math.Clamp(i, 0, destinationCount - 1) * 2, 2);

                if (InclusiveRange.Intersects(sr, dr))
                {
                    InclusiveRange.Intersection(sr, dr, scratch);
                    Debug.Assert(scratch[0] != -1);
                    Debug.Assert(scratch[1] != -1);

                    sourceCuts.Add(scratch[0], scratch[1]);
                    destinationCuts.Add(scratch[0], scratch[1]);
                }
            }
        }

        // Reset the sets to their original bounds
        for (var i = 0; i < sourceCuts.Count * 2; i++)
        {
            source[i] += sourceOffset;
        }

        for (var i = 0; i < destinationCuts.Count * 2; i++)
        {
            destination[i] += destinationOffset;
        }

        // Return the number of ranges we have after distributing cuts
        Debug.Assert(sourceCuts.Count == destinationCuts.Count);
        return sourceCuts.Count;
    }
}