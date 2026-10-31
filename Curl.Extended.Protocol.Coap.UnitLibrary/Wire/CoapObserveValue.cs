namespace Curl.Extended.Protocol.Coap.Wire;

/// <summary>
/// The value of the Observe option: in a request, 0 registers and 1 deregisters; in a notification,
/// a 24-bit sequence number; in 0 to 3 bytes (RFC 7641 section 2).
/// </summary>
internal static class CoapObserveValue
{
    /// <summary>The request value that registers an observation.</summary>
    public const uint Register = 0;

    /// <summary>The request value that cancels an observation.</summary>
    public const uint Deregister = 1;

    /// <summary>The largest sequence number three bytes hold.</summary>
    public const uint MaximumSequenceNumber = (1u << 24) - 1;

    /// <summary>The option value bytes of <paramref name="value"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is past 2^24 - 1.</exception>
    public static byte[] Encode(uint value)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaximumSequenceNumber, nameof(value));
        return CoapUnsignedInteger.Encode(value);
    }

    /// <summary>The value an Observe option's value bytes hold.</summary>
    /// <exception cref="FormatException">The value is longer than 3 bytes.</exception>
    public static uint Decode(ReadOnlySpan<byte> value) => CoapUnsignedInteger.Decode(value, 3);
}
