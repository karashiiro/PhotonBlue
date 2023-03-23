using System.Diagnostics;

namespace PhotonBlue.PRS;

public static class InclusiveRangeUtils
{
    /// <summary>
    /// Given two inclusive ranges and a cut position, aligns the ranges
    /// over the cut, such that the ranges are divided into sub-ranges of
    /// aligned lengths. The input ranges must be of exactly equal length.
    ///
    /// This algorithm is used to compute the optimal ranges for block-
    /// copying regions of the circular lookaround buffer used in the PRS
    /// decoder. Copies need to be done such that the source region does
    /// not overwrite itself while copying to the destination, and such
    /// that copies can be split at the end of the circular buffer, wrapping
    /// around to the start again.
    ///
    /// For example:
    /// Given the ranges [8188, 8195] and [8191, 8198], and a cut position
    /// of 8191, a cut is made into the first range, since it crosses the
    /// cut.
    ///
    /// This gives us [[8188, 8190], [8191, 8195]] and [[8191, 8198]]. Note
    /// that neither range in the first set crosses the cut.
    ///
    /// Next, the sets are aligned by setting them to begin at 0. This gives
    /// us [[0, 2], [3, 7]] and [[0, 7]].
    ///
    /// To get sub-ranges of aligned lengths in the result, the cuts now need
    /// to be shared between the sets. The range [0, 2] intersects with the
    /// range [0, 7], splitting it into [[0, 2], [3, 7]], which is the same
    /// as the source set.
    ///
    /// Finally, the offset is added back to each set, giving us [[8188, 8190],
    /// [8191, 8195]] and [[8191, 8193], [8194, 8198]]. The first range of each
    /// set has a length of 3 (the ranges are inclusive), and the second range
    /// of each set has a length of 5, so they are aligned.
    /// </summary>
    /// <param name="source">
    /// The source range buffer, which must have a length of 6.
    /// The initial range must be in the first two positions.
    /// </param>
    /// <param name="destination">
    /// The destination range buffer, which must have a length of 6.
    /// The initial range must be in the first two positions.
    /// </param>
    /// <param name="cut">The position of the initial cut.</param>
    /// <returns>The number of intervals in the result.</returns>
    public static int AlignRangesOverCut(Span<int> source, Span<int> destination, int cut)
    {
        // We can have at most three ranges (six ints) for each part.
        // This can be improved to only use four ints by inferring the remaining
        // cuts, but that doesn't seem worth the effort.
        Debug.Assert(source.Length == 6);
        Debug.Assert(destination.Length == 6);
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
            source[3] = source[1];
            source[1] = cut - 1;
            source[2] = cut;
            sourceCuts.Count++;
        }

        if (InclusiveRange.Contains(destination[..2], cut))
        {
            destination[3] = destination[1];
            destination[1] = cut - 1;
            destination[2] = cut;
            destinationCuts.Count++;
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
        Debug.Assert(sourceCuts.Count < 4);
        return sourceCuts.Count;
    }
}