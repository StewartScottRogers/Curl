namespace Curl.Quic;

/// <summary>
/// Packet number encoding and decoding (RFC 9000 section 17.1 and Appendix A): a packet
/// carries only the least significant 1 to 4 bytes of its packet number, enough for the
/// receiver to rebuild the whole number from the largest one it has seen.
/// </summary>
public static class QuicPacketNumber
{
    /// <summary>
    /// Gets how many bytes a packet number needs on the wire (RFC 9000 Appendix A.2): enough
    /// to represent more than twice the number of packets not yet acknowledged.
    /// </summary>
    /// <param name="fullPacketNumber">The packet number being sent.</param>
    /// <param name="largestAcknowledged">The largest packet number the peer has acknowledged in this space, or <see langword="null" /> when none has been.</param>
    /// <returns>1, 2, 3 or 4.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="largestAcknowledged" /> is not below <paramref name="fullPacketNumber" />, or more than 2^31 packets are unacknowledged.</exception>
    public static int GetEncodedLength(ulong fullPacketNumber, ulong? largestAcknowledged)
    {
        if (largestAcknowledged >= fullPacketNumber)
        {
            throw new ArgumentOutOfRangeException(nameof(largestAcknowledged), largestAcknowledged, "The largest acknowledged packet number must be below the one being sent.");
        }

        var unacknowledged = fullPacketNumber - (largestAcknowledged ?? ulong.MaxValue);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(unacknowledged, 1UL << 31, nameof(fullPacketNumber));
        var length = 1;
        while (unacknowledged > 1UL << (8 * length - 1))
        {
            length++;
        }

        return length;
    }

    /// <summary>
    /// Truncates a packet number to the bytes a packet carries.
    /// </summary>
    /// <param name="fullPacketNumber">The packet number.</param>
    /// <param name="length">1, 2, 3 or 4.</param>
    /// <returns>Its least significant <paramref name="length" /> bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length" /> is not 1 to 4.</exception>
    public static uint Truncate(ulong fullPacketNumber, int length)
    {
        RequireLength(length);
        return (uint)(fullPacketNumber & Mask(length));
    }

    /// <summary>
    /// Rebuilds a packet number from its truncated form (RFC 9000 Appendix A.3): the value
    /// ending in <paramref name="truncatedPacketNumber" /> closest to one more than
    /// <paramref name="largestPacketNumber" />.
    /// </summary>
    /// <param name="largestPacketNumber">The largest packet number successfully processed in this space, or <see langword="null" /> when none has been.</param>
    /// <param name="truncatedPacketNumber">The packet number bytes the packet carried.</param>
    /// <param name="length">How many bytes it carried, 1 to 4.</param>
    /// <returns>The full packet number.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length" /> is not 1 to 4.</exception>
    public static ulong Decode(ulong? largestPacketNumber, uint truncatedPacketNumber, int length)
    {
        RequireLength(length);
        var expected = largestPacketNumber + 1 ?? 0;
        var window = 1UL << (8 * length);
        var halfWindow = window / 2;
        var candidate = (expected & ~Mask(length)) | (truncatedPacketNumber & Mask(length));
        if (candidate + halfWindow <= expected && candidate < (1UL << 62) - window)
        {
            return candidate + window;
        }

        if (candidate > expected + halfWindow && candidate >= window)
        {
            return candidate - window;
        }

        return candidate;
    }

    private static ulong Mask(int length) => (1UL << (8 * length)) - 1;

    private static void RequireLength(int length)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, 4);
    }
}
