namespace Curl.Extension.WireGuard.Messages;

/// <summary>
/// The four-byte header every WireGuard message starts with - the type, then three reserved
/// zero bytes - and the fixed-length field writes the message encoders share.
/// </summary>
internal static class WireGuardMessageLayout
{
    /// <summary>The length of the type byte and the three reserved bytes.</summary>
    public const int HeaderSize = 4;

    /// <summary>Writes <paramref name="type" /> and three zero bytes at the start of <paramref name="message" />.</summary>
    public static void WriteHeader(WireGuardMessageType type, Span<byte> message)
    {
        message[0] = (byte)type;
        message[1..HeaderSize].Clear();
    }

    /// <summary>
    /// Whether <paramref name="datagram" /> starts with the header of <paramref name="type" />:
    /// at least four bytes, the type byte, then three zero bytes.
    /// </summary>
    public static bool StartsWithHeader(ReadOnlySpan<byte> datagram, WireGuardMessageType type) =>
        datagram.Length >= HeaderSize
        && datagram[0] == (byte)type
        && !datagram[1..HeaderSize].ContainsAnyExcept((byte)0);

    /// <summary>Copies <paramref name="value" /> into <paramref name="field" />, which it must fill exactly.</summary>
    /// <exception cref="ArgumentException"><paramref name="value" /> is not the field's length.</exception>
    public static void WriteField(ReadOnlySpan<byte> value, Span<byte> field, string name)
    {
        if (value.Length != field.Length)
        {
            throw new ArgumentException($"{name} is {field.Length} bytes; the value given is {value.Length}.", name);
        }

        value.CopyTo(field);
    }
}
