using System.Buffers.Binary;
using Curl.Tls;

namespace Curl.Extended.Protocol.Coap.Dtls.Wire;

/// <summary>
/// The 12-byte header of a DTLS handshake message fragment (RFC 6347 section 4.2.2): the
/// message type, the whole message's length, its message_seq, and where this fragment sits
/// in the message.
/// </summary>
/// <param name="MessageType">The handshake message type.</param>
/// <param name="Length">The whole message body's length, 24 bits.</param>
/// <param name="MessageSequence">message_seq, counting the handshake messages of one handshake.</param>
/// <param name="FragmentOffset">Where the fragment starts in the message body, 24 bits.</param>
/// <param name="FragmentLength">The fragment's length, 24 bits.</param>
internal readonly record struct DtlsHandshakeHeader(HandshakeType MessageType, int Length, ushort MessageSequence, int FragmentOffset, int FragmentLength)
{
    /// <summary>The header's size in bytes.</summary>
    public const int HeaderLength = 12;

    /// <summary>The largest 24-bit value.</summary>
    public const int MaximumUInt24 = (1 << 24) - 1;

    /// <summary>Writes the 12 header bytes.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A length or offset is outside 0 to 2^24 - 1, or the fragment runs past the message.</exception>
    public byte[] Encode()
    {
        ThrowIfNotUInt24(Length, nameof(Length));
        ThrowIfNotUInt24(FragmentOffset, nameof(FragmentOffset));
        ThrowIfNotUInt24(FragmentLength, nameof(FragmentLength));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(FragmentOffset + FragmentLength, Length, nameof(FragmentLength));
        byte[] header = new byte[HeaderLength];
        header[0] = (byte)MessageType;
        WriteUInt24(header.AsSpan(1), Length);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4), MessageSequence);
        WriteUInt24(header.AsSpan(6), FragmentOffset);
        WriteUInt24(header.AsSpan(9), FragmentLength);
        return header;
    }

    /// <summary>Reads the header at the start of <paramref name="bytes" />.</summary>
    /// <exception cref="FormatException">There are fewer than 12 bytes, or the fragment runs past the message's length.</exception>
    public static DtlsHandshakeHeader Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < HeaderLength)
        {
            throw new FormatException($"A DTLS handshake fragment of {bytes.Length} bytes is shorter than its 12-byte header.");
        }

        DtlsHandshakeHeader header = new(
            (HandshakeType)bytes[0],
            ReadUInt24(bytes[1..]),
            BinaryPrimitives.ReadUInt16BigEndian(bytes[4..]),
            ReadUInt24(bytes[6..]),
            ReadUInt24(bytes[9..]));
        if (header.FragmentOffset + header.FragmentLength > header.Length)
        {
            throw new FormatException($"A DTLS handshake fragment at {header.FragmentOffset} of {header.FragmentLength} bytes runs past its {header.Length}-byte message.");
        }

        return header;
    }

    private static void ThrowIfNotUInt24(int value, string name)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value, name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaximumUInt24, name);
    }

    private static void WriteUInt24(Span<byte> destination, int value)
    {
        destination[0] = (byte)(value >> 16);
        destination[1] = (byte)(value >> 8);
        destination[2] = (byte)value;
    }

    private static int ReadUInt24(ReadOnlySpan<byte> source) => (source[0] << 16) | (source[1] << 8) | source[2];
}
