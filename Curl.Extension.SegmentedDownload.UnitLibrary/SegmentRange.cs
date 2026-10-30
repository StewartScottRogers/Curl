namespace Curl.Extension.SegmentedDownload;

/// <summary>One <c>int-range</c> of RFC 9110 §14.1.1: the bytes from <paramref name="First" /> to <paramref name="Last" />, both included.</summary>
/// <param name="First">The offset of the first byte.</param>
/// <param name="Last">The offset of the last byte.</param>
internal sealed record SegmentRange(long First, long Last)
{
    /// <summary>Gets how many bytes the range holds.</summary>
    public long Length => Last - First + 1;

    /// <summary>Gets the <c>Range</c> request header field asking for this range alone (RFC 9110 §14.2).</summary>
    public string RangeHeader => $"Range: bytes={First}-{Last}";

    /// <summary>Splits a representation of <paramref name="completeLength" /> bytes into at most <paramref name="count" /> ranges of nearly equal length, in order.</summary>
    /// <param name="completeLength">The representation's length, at least 1.</param>
    /// <param name="count">How many ranges to make, at least 1; fewer when the representation has fewer bytes.</param>
    /// <returns>Consecutive ranges covering every byte once.</returns>
    public static IReadOnlyList<SegmentRange> Split(long completeLength, int count)
    {
        var segments = (int)Math.Min(count, completeLength);
        var shortLength = completeLength / segments;
        var longerSegments = completeLength % segments;
        var ranges = new SegmentRange[segments];
        long first = 0;
        for (var index = 0; index < segments; index++)
        {
            var length = shortLength + (index < longerSegments ? 1 : 0);
            ranges[index] = new SegmentRange(first, first + length - 1);
            first += length;
        }

        return ranges;
    }
}
