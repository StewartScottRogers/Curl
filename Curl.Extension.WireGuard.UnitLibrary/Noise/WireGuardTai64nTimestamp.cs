using System.Buffers.Binary;

namespace Curl.Extension.WireGuard.Noise;

/// <summary>
/// The 12-byte TAI64N timestamp an initiation carries (whitepaper section 5.4.2): 8 bytes of
/// big-endian seconds offset by 2^62 + 10, then 4 bytes of big-endian nanoseconds, as the Linux
/// module and wireguard-go write them. The nanoseconds are rounded down to a multiple of
/// 2^24 (about 16.8 ms), as both do, so a peer can tell no more than that of the clock and
/// the responder's replay check also limits initiations to about 60 a second.
/// </summary>
internal static class WireGuardTai64nTimestamp
{
    /// <summary>The length of a timestamp.</summary>
    public const int Size = 12;

    private const ulong Tai64Base = 0x400000000000000AUL;

    private const uint NanosecondMask = ~((1u << 24) - 1);

    /// <summary>Writes the timestamp of <paramref name="now" /> into <paramref name="destination" />.</summary>
    public static void Write(DateTimeOffset now, Span<byte> destination)
    {
        var unixSeconds = now.ToUnixTimeSeconds();
        var nanoseconds = (uint)(now.UtcTicks % TimeSpan.TicksPerSecond) * 100u;
        BinaryPrimitives.WriteUInt64BigEndian(destination[..8], Tai64Base + (ulong)unixSeconds);
        BinaryPrimitives.WriteUInt32BigEndian(destination[8..Size], nanoseconds & NanosecondMask);
    }
}
