namespace Curl.Conformance.Extended.Coap;

/// <summary>
/// A CoAP message as RFC 7252 section 3 lays it out, with the reference server's own encoder and
/// decoder: the 4-byte header (version, type, token length, code, Message ID), the token, the options
/// in delta and length encoding with the 13 and 14 extended forms (section 3.1), and the 0xFF payload
/// marker before a non-empty payload.
/// </summary>
/// <param name="Version">The version field (1; any other only to send a message curl must ignore).</param>
/// <param name="Type">The message type.</param>
/// <param name="Code">The code byte, class in the top 3 bits and detail in the low 5.</param>
/// <param name="MessageId">The Message ID.</param>
/// <param name="Token">The token, 0 to 8 bytes.</param>
/// <param name="Options">The options, encoded in ascending number.</param>
/// <param name="Payload">The payload; empty for none.</param>
public sealed record CoapReferenceMessage(int Version, CoapReferenceMessageType Type, byte Code, ushort MessageId, byte[] Token, IReadOnlyList<CoapReferenceOption> Options, byte[] Payload)
{
    private const byte PayloadMarker = 0xFF;

    /// <summary>Writes the message's bytes.</summary>
    /// <returns>The datagram.</returns>
    public byte[] Encode()
    {
        List<byte> bytes = [(byte)((Version << 6) | ((int)Type << 4) | Token.Length), Code, (byte)(MessageId >> 8), (byte)MessageId, .. Token];
        int previous = 0;
        foreach (CoapReferenceOption option in Options.OrderBy(option => option.Number))
        {
            int delta = option.Number - previous;
            previous = option.Number;
            bytes.Add((byte)((Nibble(delta) << 4) | Nibble(option.Value.Length)));
            bytes.AddRange(Extension(delta));
            bytes.AddRange(Extension(option.Value.Length));
            bytes.AddRange(option.Value);
        }

        if (Payload.Length > 0)
        {
            bytes.Add(PayloadMarker);
            bytes.AddRange(Payload);
        }

        return [.. bytes];
    }

    /// <summary>Reads a message.</summary>
    /// <param name="datagram">The datagram.</param>
    /// <returns>The message.</returns>
    /// <exception cref="FormatException">A token length over 8, an option nibble of 15, or a payload marker with no payload (section 3).</exception>
    /// <exception cref="IndexOutOfRangeException">The datagram ends inside its header, token or an option.</exception>
    public static CoapReferenceMessage Decode(ReadOnlySpan<byte> datagram)
    {
        int tokenLength = datagram[0] & 0x0F;
        if (tokenLength > 8)
        {
            throw new FormatException($"CoAP token length {tokenLength} is reserved.");
        }

        int position = 4 + tokenLength;
        List<CoapReferenceOption> options = ReadOptions(datagram, ref position);
        byte[] payload = ReadPayload(datagram, position);
        return new CoapReferenceMessage(
            datagram[0] >> 6,
            (CoapReferenceMessageType)((datagram[0] >> 4) & 0x03),
            datagram[1],
            (ushort)((datagram[2] << 8) | datagram[3]),
            datagram.Slice(4, tokenLength).ToArray(),
            options,
            payload);
    }

    private static List<CoapReferenceOption> ReadOptions(ReadOnlySpan<byte> datagram, ref int position)
    {
        List<CoapReferenceOption> options = [];
        int number = 0;
        while (position < datagram.Length && datagram[position] != PayloadMarker)
        {
            byte first = datagram[position++];
            number += ExtendedValue(first >> 4, datagram, ref position);
            int length = ExtendedValue(first & 0x0F, datagram, ref position);
            options.Add(new CoapReferenceOption(number, datagram.Slice(position, length).ToArray()));
            position += length;
        }

        return options;
    }

    private static byte[] ReadPayload(ReadOnlySpan<byte> datagram, int position)
    {
        if (position >= datagram.Length)
        {
            return [];
        }

        return position + 1 < datagram.Length
            ? datagram[(position + 1)..].ToArray()
            : throw new FormatException("CoAP payload marker is followed by no payload.");
    }

    private static int Nibble(int value) => value < 13 ? value : value < 269 ? 13 : 14;

    private static byte[] Extension(int value) =>
        value < 13 ? [] : value < 269 ? [(byte)(value - 13)] : [(byte)((value - 269) >> 8), (byte)(value - 269)];

    private static int ExtendedValue(int nibble, ReadOnlySpan<byte> datagram, ref int position)
    {
        switch (nibble)
        {
            case < 13:
                return nibble;
            case 13:
                return datagram[position++] + 13;
            case 14:
                int value = ((datagram[position] << 8) | datagram[position + 1]) + 269;
                position += 2;
                return value;
            default:
                throw new FormatException("CoAP option nibble 15 is reserved outside the payload marker.");
        }
    }
}
