using System.Buffers.Binary;

namespace Curl.Extended.Protocol.Coap.Wire;

/// <summary>
/// Writes and reads CoAP messages in the binary format of RFC 7252 section 3: the 4-byte header,
/// the token, the options with delta and length encoding (section 3.1), and the 0xFF payload marker
/// before a payload.
/// </summary>
internal static class CoapMessageEncoding
{
    /// <summary>The only protocol version RFC 7252 defines.</summary>
    public const int Version = 1;

    /// <summary>The byte that ends the options and starts the payload.</summary>
    public const byte PayloadMarker = 0xFF;

    /// <summary>The longest token.</summary>
    public const int MaximumTokenLength = 8;

    /// <summary>The largest option delta or length the 14 extended form holds: 65535 + 269.</summary>
    public const int MaximumExtendedValue = 65535 + 269;

    private const int OneByteExtension = 13;
    private const int TwoByteExtension = 14;
    private const int OneByteBase = 13;
    private const int TwoByteBase = 269;
    private const int ReservedNibble = 15;

    /// <summary>
    /// The bytes of <paramref name="message"/>, its options written in ascending number (options
    /// that share a number keep their order), and the payload marker only before a payload.
    /// </summary>
    /// <exception cref="ArgumentException">The token is longer than 8 bytes, or an option's number or length is out of range.</exception>
    public static byte[] Encode(CoapMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Token.Length > MaximumTokenLength)
        {
            throw new ArgumentException($"A CoAP token of {message.Token.Length} bytes is longer than {MaximumTokenLength}.", nameof(message));
        }

        using MemoryStream output = new();
        output.WriteByte((byte)((Version << 6) | ((int)message.Type << 4) | message.Token.Length));
        output.WriteByte(message.Code.Value);
        output.WriteByte((byte)(message.MessageId >> 8));
        output.WriteByte((byte)message.MessageId);
        output.Write(message.Token);
        WriteOptions(output, message.Options);
        if (message.Payload.Length > 0)
        {
            output.WriteByte(PayloadMarker);
            output.Write(message.Payload);
        }

        return output.ToArray();
    }

    /// <summary>The message <paramref name="datagram"/> holds.</summary>
    /// <exception cref="FormatException">
    /// The datagram is a message format error by RFC 7252 section 3: shorter than the header, a version
    /// other than 1, a token length of 9 to 15, an Empty message with anything after its header, an
    /// option delta or length nibble of 15 that is not the payload marker, an option or token past the
    /// end, or a payload marker with no payload after it.
    /// </exception>
    public static CoapMessage Decode(ReadOnlySpan<byte> datagram)
    {
        if (datagram.Length < 4)
        {
            throw new FormatException($"A CoAP message of {datagram.Length} bytes is shorter than its 4-byte header.");
        }

        int version = datagram[0] >> 6;
        if (version != Version)
        {
            throw new FormatException($"CoAP version {version} is not version {Version}.");
        }

        int tokenLength = datagram[0] & 0x0F;
        if (tokenLength > MaximumTokenLength)
        {
            throw new FormatException($"A CoAP token length of {tokenLength} is reserved.");
        }

        CoapCode code = new(datagram[1]);
        if (code == CoapCode.Empty && datagram.Length > 4)
        {
            throw new FormatException("A CoAP Empty message has bytes after its header.");
        }

        ReadOnlySpan<byte> rest = datagram[4..];
        byte[] token = Take(ref rest, tokenLength, "token").ToArray();
        List<CoapOption> options = [];
        byte[] payload = ReadOptions(ref rest, options);
        return new CoapMessage(
            (CoapMessageType)((datagram[0] >> 4) & 0x03),
            code,
            BinaryPrimitives.ReadUInt16BigEndian(datagram[2..]),
            token,
            options,
            payload);
    }

    private static void WriteOptions(MemoryStream output, IReadOnlyList<CoapOption> options)
    {
        int previousNumber = 0;
        foreach (CoapOption option in options.OrderBy(option => option.Number))
        {
            if ((uint)option.Number > ushort.MaxValue)
            {
                throw new ArgumentException($"CoAP option number {option.Number} is outside 0 to 65535.", nameof(options));
            }

            if (option.Value.Length > MaximumExtendedValue)
            {
                throw new ArgumentException($"A CoAP option value of {option.Value.Length} bytes is longer than {MaximumExtendedValue}.", nameof(options));
            }

            int delta = option.Number - previousNumber;
            previousNumber = option.Number;
            output.WriteByte((byte)((NibbleOf(delta) << 4) | NibbleOf(option.Value.Length)));
            WriteExtension(output, delta);
            WriteExtension(output, option.Value.Length);
            output.Write(option.Value);
        }
    }

    private static int NibbleOf(int value) => value switch
    {
        < OneByteBase => value,
        < TwoByteBase => OneByteExtension,
        _ => TwoByteExtension,
    };

    private static void WriteExtension(MemoryStream output, int value)
    {
        if (value >= TwoByteBase)
        {
            output.WriteByte((byte)((value - TwoByteBase) >> 8));
            output.WriteByte((byte)(value - TwoByteBase));
        }
        else if (value >= OneByteBase)
        {
            output.WriteByte((byte)(value - OneByteBase));
        }
    }

    private static byte[] ReadOptions(ref ReadOnlySpan<byte> rest, List<CoapOption> options)
    {
        int number = 0;
        while (rest.Length > 0)
        {
            byte first = rest[0];
            rest = rest[1..];
            if (first == PayloadMarker)
            {
                return ReadPayload(rest);
            }

            number += ReadExtended(ref rest, first >> 4, "delta");
            int length = ReadExtended(ref rest, first & 0x0F, "length");
            options.Add(new CoapOption(number, Take(ref rest, length, "option value").ToArray()));
        }

        return [];
    }

    private static byte[] ReadPayload(ReadOnlySpan<byte> rest)
    {
        if (rest.IsEmpty)
        {
            throw new FormatException("A CoAP payload marker is followed by no payload.");
        }

        return rest.ToArray();
    }

    private static int ReadExtended(ref ReadOnlySpan<byte> rest, int nibble, string field) => nibble switch
    {
        OneByteExtension => OneByteBase + Take(ref rest, 1, field)[0],
        TwoByteExtension => TwoByteBase + BinaryPrimitives.ReadUInt16BigEndian(Take(ref rest, 2, field)),
        ReservedNibble => throw new FormatException($"A CoAP option {field} nibble of 15 is reserved."),
        _ => nibble,
    };

    private static ReadOnlySpan<byte> Take(ref ReadOnlySpan<byte> rest, int count, string field)
    {
        if (rest.Length < count)
        {
            throw new FormatException($"A CoAP message ends inside its {field}.");
        }

        ReadOnlySpan<byte> taken = rest[..count];
        rest = rest[count..];
        return taken;
    }
}
