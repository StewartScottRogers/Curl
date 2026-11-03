namespace Curl.Conformance.Extended.Mqtt5.Transports;

/// <summary>
/// One RFC 6455 section 5.2 frame as the MQTT 5 WebSocket reference server (BL-4486) reads it from curl or
/// writes it to curl, written from the RFC: the reference server's own codec, not the client's.
/// </summary>
/// <param name="IsFinal">The FIN bit.</param>
/// <param name="ReservedBits">RSV1 to RSV3, as the three bits below FIN.</param>
/// <param name="Opcode">The opcode.</param>
/// <param name="MaskingKey">The masking key, or null when the MASK bit is clear.</param>
/// <param name="Payload">The payload, unmasked.</param>
public sealed record Mqtt5WebSocketFrame(bool IsFinal, int ReservedBits, int Opcode, byte[]? MaskingKey, byte[] Payload)
{
    /// <summary>The continuation frame opcode.</summary>
    public const int Continuation = 0x0;

    /// <summary>The text frame opcode.</summary>
    public const int Text = 0x1;

    /// <summary>The binary frame opcode.</summary>
    public const int Binary = 0x2;

    /// <summary>The close frame opcode.</summary>
    public const int Close = 0x8;

    /// <summary>The ping frame opcode.</summary>
    public const int Ping = 0x9;

    /// <summary>The pong frame opcode.</summary>
    public const int Pong = 0xA;

    /// <summary>Gets the bytes of one frame with a payload shorter than 65536 bytes.</summary>
    /// <param name="opcode">The opcode.</param>
    /// <param name="payload">The payload, unmasked.</param>
    /// <param name="isFinal">The FIN bit.</param>
    /// <param name="maskingKey">Four bytes to mask the payload with, or null for an unmasked frame (a server's).</param>
    /// <param name="reservedBits">RSV1 to RSV3; 0 unless an extension was negotiated.</param>
    /// <returns>The frame header and the (masked) payload.</returns>
    public static byte[] Encode(int opcode, byte[] payload, bool isFinal = true, byte[]? maskingKey = null, int reservedBits = 0)
    {
        ArgumentNullException.ThrowIfNull(payload);
        List<byte> frame = [(byte)((isFinal ? 0x80 : 0) | (reservedBits << 4) | opcode)];
        int maskBit = maskingKey is null ? 0 : 0x80;
        if (payload.Length < 126)
        {
            frame.Add((byte)(maskBit | payload.Length));
        }
        else
        {
            frame.AddRange([(byte)(maskBit | 126), (byte)(payload.Length >> 8), (byte)payload.Length]);
        }

        frame.AddRange(maskingKey ?? []);
        frame.AddRange(Unmask(payload, maskingKey));
        return [.. frame];
    }

    /// <summary>Takes the first whole frame off the front of <paramref name="pending" />.</summary>
    /// <param name="pending">Bytes read and not yet framed; the frame's bytes are removed from it.</param>
    /// <returns>The frame, or null while <paramref name="pending" /> holds less than a whole one.</returns>
    public static Mqtt5WebSocketFrame? TakeFrom(List<byte> pending)
    {
        ArgumentNullException.ThrowIfNull(pending);
        (int headerLength, long length) = pending.Count < 2 ? (int.MaxValue, 0L) : Measure(pending);
        if (pending.Count < headerLength || pending.Count - headerLength < length)
        {
            return null;
        }

        byte[]? maskingKey = (pending[1] & 0x80) != 0 ? [.. pending.GetRange(headerLength - 4, 4)] : null;
        byte[] payload = Unmask([.. pending.GetRange(headerLength, (int)length)], maskingKey);
        byte first = pending[0];
        pending.RemoveRange(0, headerLength + (int)length);
        return new Mqtt5WebSocketFrame((first & 0x80) != 0, (first >> 4) & 0x07, first & 0x0F, maskingKey, payload);
    }

    // The header's length (with the masking key) and, once the header is whole, the payload's length.
    private static (int HeaderLength, long Length) Measure(List<byte> pending)
    {
        int shortLength = pending[1] & 0x7F;
        int lengthBytes = shortLength switch { 126 => 2, 127 => 8, _ => 0 };
        int headerLength = 2 + lengthBytes + ((pending[1] & 0x80) != 0 ? 4 : 0);
        return (headerLength, pending.Count < headerLength ? 0 : PayloadLength(pending, shortLength, lengthBytes));
    }

    private static long PayloadLength(List<byte> pending, int shortLength, int lengthBytes)
    {
        long length = lengthBytes == 0 ? shortLength : 0;
        for (int index = 0; index < lengthBytes; index++)
        {
            length = (length << 8) | pending[2 + index];
        }

        return length;
    }

    private static byte[] Unmask(byte[] payload, byte[]? maskingKey)
    {
        byte[] unmasked = [.. payload];
        for (int index = 0; maskingKey is not null && index < unmasked.Length; index++)
        {
            unmasked[index] ^= maskingKey[index % 4];
        }

        return unmasked;
    }
}
