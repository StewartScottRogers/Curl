namespace Curl.Http2;

/// <summary>
/// One flow-control window (RFC 9113 section 5.2): how many DATA bytes may still be sent
/// on a stream or the connection. A change of SETTINGS_INITIAL_WINDOW_SIZE can drive it
/// below zero (RFC 9113 section 6.9.2), so it is held as a signed 64-bit count.
/// </summary>
public sealed class Http2FlowControlWindow
{
    /// <summary>The largest a window may grow: 2^31 - 1 (RFC 9113 section 6.9.1).</summary>
    public const int MaximumSize = int.MaxValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="Http2FlowControlWindow" /> class.
    /// </summary>
    /// <param name="initialSize">The window's starting size.</param>
    public Http2FlowControlWindow(int initialSize)
    {
        Size = initialSize;
    }

    /// <summary>Gets the window's size, which may be negative.</summary>
    public long Size { get; private set; }

    /// <summary>Gets how many bytes may be sent now: the size, or 0 while it is negative.</summary>
    public int Available => (int)Math.Max(0, Size);

    /// <summary>
    /// Takes <paramref name="count" /> bytes out of the window, unless that would overrun it.
    /// </summary>
    /// <param name="count">The DATA frame's full payload length, padding included.</param>
    /// <returns><see langword="false" />, leaving the window unchanged, when <paramref name="count" /> exceeds it.</returns>
    public bool TryConsume(int count)
    {
        if (count > Size)
        {
            return false;
        }

        Size -= count;
        return true;
    }

    /// <summary>
    /// Grows the window by <paramref name="delta" />, or shrinks it when negative, unless it
    /// would pass <see cref="MaximumSize" />.
    /// </summary>
    /// <param name="delta">A WINDOW_UPDATE increment, or a change of SETTINGS_INITIAL_WINDOW_SIZE.</param>
    /// <returns><see langword="false" />, leaving the window unchanged, when it would pass the maximum.</returns>
    public bool TryAdjust(long delta)
    {
        if (Size + delta > MaximumSize)
        {
            return false;
        }

        Size += delta;
        return true;
    }
}
