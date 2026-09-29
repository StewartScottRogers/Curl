namespace Curl.Zstandard;

/// <summary>
/// Executes a block's sequences (RFC 8878 section 3.1.1.4): each copies literals, then a
/// match from earlier output - in this block, or in the frame's history before it - into
/// the block's output, and whatever literals are left follow the last sequence.
/// </summary>
internal ref struct ZstandardSequenceWriter(ReadOnlySpan<byte> literals, Span<byte> output, ZstandardHistory history)
{
    private readonly Span<byte> output = output;

    private readonly ZstandardHistory history = history;

    private ReadOnlySpan<byte> literals = literals;

    /// <summary>How many bytes of the block's output are written.</summary>
    public int Written { get; private set; }

    /// <summary>Copies the next <paramref name="length" /> literals; <see langword="false" /> when there are not that many, or no room for them.</summary>
    public bool CopyLiterals(int length)
    {
        if (length > literals.Length || length > output.Length - Written)
        {
            return false;
        }

        literals[..length].CopyTo(output[Written..]);
        literals = literals[length..];
        Written += length;
        return true;
    }

    /// <summary>Copies the literals no sequence took; <see langword="false" /> when there is no room for them.</summary>
    public bool CopyRemainingLiterals() => CopyLiterals(literals.Length);

    /// <summary>
    /// Copies <paramref name="length" /> bytes from <paramref name="offset" /> bytes back;
    /// <see langword="false" /> when the offset is 0, reaches before the frame's content or
    /// further back than its history holds, or the match has no room.
    /// </summary>
    public bool CopyMatch(long offset, int length)
    {
        if (offset < 1 || offset > (long)Written + history.Count || length > output.Length - Written)
        {
            return false;
        }

        var fromHistory = (int)Math.Clamp(offset - Written, 0, length);
        if (fromHistory > 0)
        {
            history.CopyTo((int)offset - Written, output.Slice(Written, fromHistory));
        }

        CopyWithinOutput(Written + fromHistory, Written + length, (int)offset);
        Written += length;
        return true;
    }

    /// <summary>
    /// Fills <paramref name="start" /> to <paramref name="end" /> from <paramref name="offset" />
    /// bytes back, where the match may overlap itself: each copy doubles the run already
    /// repeated, so no copy overlaps its own source.
    /// </summary>
    private readonly void CopyWithinOutput(int start, int end, int offset)
    {
        var source = start - offset;
        var position = start;
        while (position < end)
        {
            var count = Math.Min(end - position, position - source);
            output.Slice(source, count).CopyTo(output[position..]);
            position += count;
        }
    }
}
