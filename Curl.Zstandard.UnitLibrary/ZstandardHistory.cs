namespace Curl.Zstandard;

/// <summary>
/// The latest bytes of a frame's content, which the matches of later blocks copy from: as
/// many as libzstd's streaming decoder buffers, <c>Window_Size</c> (RFC 8878 section
/// 3.1.1.1.2) plus two <c>Block_Maximum_Size</c> plus 64 bytes (ADR-0196). It is a ring buffer that grows
/// as content arrives, up to its capacity, so a small frame with a large declared window
/// allocates only what it uses.
/// </summary>
internal sealed class ZstandardHistory
{
    /// <summary>The first allocation, unless the capacity is smaller.</summary>
    private const int InitialCapacity = 64 * 1024;

    private byte[] buffer = [];

    /// <summary>Where the next byte goes in <see cref="buffer" />.</summary>
    private int end;

    private int capacity;

    /// <summary>How many of the frame's latest bytes are held.</summary>
    public int Count { get; private set; }

    /// <summary>Empties the history for a frame that keeps up to <paramref name="bytesToKeep" /> bytes, capped at the largest array.</summary>
    public void Reset(ulong bytesToKeep)
    {
        capacity = (int)Math.Min(bytesToKeep, (ulong)Array.MaxLength);
        Count = 0;
        end = 0;
    }

    /// <summary>
    /// Adds <paramref name="content" />, which is no longer than the capacity (a block never
    /// is), after the bytes already held, dropping the oldest past the capacity.
    /// </summary>
    public void Append(ReadOnlySpan<byte> content)
    {
        Grow((int)Math.Min(capacity, (long)Count + content.Length));
        var first = Math.Min(content.Length, buffer.Length - end);
        content[..first].CopyTo(buffer.AsSpan(end));
        content[first..].CopyTo(buffer);
        end += content.Length;
        end -= end >= buffer.Length ? buffer.Length : 0;
        Count = Math.Min(Count + content.Length, buffer.Length);
    }

    /// <summary>
    /// Copies <paramref name="destination" />'s length of bytes, starting
    /// <paramref name="distance" /> bytes before the latest, which is at most
    /// <see cref="Count" /> and at least that length.
    /// </summary>
    public void CopyTo(int distance, Span<byte> destination)
    {
        var start = end - distance;
        start += start < 0 ? buffer.Length : 0;
        var first = Math.Min(destination.Length, buffer.Length - start);
        buffer.AsSpan(start, first).CopyTo(destination);
        buffer.AsSpan(0, destination.Length - first).CopyTo(destination[first..]);
    }

    /// <summary>Makes room for <paramref name="needed" /> bytes, keeping what is held in order at the start of a larger buffer.</summary>
    private void Grow(int needed)
    {
        if (needed <= buffer.Length)
        {
            return;
        }

        var grown = new byte[Math.Min(capacity, Math.Max(Math.Max(needed, InitialCapacity), 2L * buffer.Length))];
        CopyTo(Count, grown.AsSpan(0, Count));
        buffer = grown;
        end = Count;
    }
}
