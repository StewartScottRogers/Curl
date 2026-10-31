using System.Buffers.Binary;

namespace Curl.WebSocket;

/// <summary>
/// Writes one client frame as RFC 6455 requires and curl 8.21.0 sends it: the FIN bit as asked
/// (set for every frame the tool sends), the mask bit set, the shortest of the 7-, 16- and 64-bit
/// length forms, then a 4-byte mask drawn from the injected random source and the payload masked
/// with it (ADR-0128, ADR-0131). Public for libcurl's <c>curl_ws_send</c> (BL-2471), which may also
/// write a frame in parts: its head announcing the whole payload, then each piece masked at its offset
/// in the frame (<c>CURLWS_OFFSET</c> and <c>curl_ws_start_frame</c>, BL-2481).
/// </summary>
public static class WsFrameEncoder
{
    /// <summary>The longest payload the 7-bit length form carries.</summary>
    private const int Longest7BitLength = 125;

    /// <summary>The 7-bit length value that announces a 16-bit length.</summary>
    private const byte Announces16BitLength = 126;

    /// <summary>The 7-bit length value that announces a 64-bit length.</summary>
    private const byte Announces64BitLength = 127;

    private const byte FinalFragment = 0x80;

    private const byte Masked = 0x80;

    /// <summary>How many bytes a frame's mask has; a head written by <see cref="EncodeHead" /> ends with them.</summary>
    public const int MaskLength = 4;

    /// <summary>The longest payload a control frame (close, ping or pong) may carry (RFC 6455 section 5.5).</summary>
    public const int LongestControlPayloadLength = 125;

    /// <summary>
    /// Whether a frame of <paramref name="opcode" /> announcing <paramref name="payloadLength" /> bytes is a control
    /// frame longer than <see cref="LongestControlPayloadLength" />, which curl 8.22.0's <c>curl_ws_send</c> refuses
    /// with <c>CURLE_TOO_LARGE</c> before writing a byte (measured, CGR-123).
    /// </summary>
    /// <param name="opcode">What the frame carries.</param>
    /// <param name="payloadLength">The whole payload's size.</param>
    /// <returns>Whether the control frame's payload is too long.</returns>
    public static bool IsControlPayloadTooLong(WsOpcode opcode, long payloadLength) =>
        opcode is WsOpcode.Close or WsOpcode.Ping or WsOpcode.Pong && payloadLength > LongestControlPayloadLength;

    /// <summary>Writes a masked, unfragmented frame.</summary>
    /// <param name="opcode">What the frame carries.</param>
    /// <param name="payload">The payload, unmasked.</param>
    /// <param name="randomSource">Supplies the frame's 4-byte mask.</param>
    /// <returns>The frame's bytes, ready to send.</returns>
    public static byte[] Encode(WsOpcode opcode, ReadOnlySpan<byte> payload, IWebSocketRandomSource randomSource) =>
        Encode(opcode, isFinal: true, payload, randomSource);

    /// <summary>
    /// Writes a masked frame of any opcode, with the FIN bit clear for a fragment that more
    /// fragments follow: a first fragment's opcode is <see cref="WsOpcode.Text" /> or
    /// <see cref="WsOpcode.Binary" />, and every later one's <see cref="WsOpcode.Continuation" />.
    /// </summary>
    /// <param name="opcode">What the frame carries.</param>
    /// <param name="isFinal">Whether the FIN bit is set: the frame ends its message.</param>
    /// <param name="payload">The payload, unmasked.</param>
    /// <param name="randomSource">Supplies the frame's 4-byte mask.</param>
    /// <returns>The frame's bytes, ready to send.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="randomSource" /> is <see langword="null" />.</exception>
    public static byte[] Encode(WsOpcode opcode, bool isFinal, ReadOnlySpan<byte> payload, IWebSocketRandomSource randomSource)
    {
        byte[] head = EncodeHead(opcode, isFinal, payload.Length, randomSource);
        byte[] frame = new byte[head.Length + payload.Length];
        head.CopyTo(frame, 0);
        MaskInto(payload, head.AsSpan(head.Length - MaskLength), 0, frame.AsSpan(head.Length));
        return frame;
    }

    /// <summary>
    /// Writes a masked frame's head alone: the first byte, the length announcing
    /// <paramref name="payloadLength" /> bytes, and last the <see cref="MaskLength" />-byte mask drawn from
    /// <paramref name="randomSource" />, with which <see cref="MaskPart" /> masks the payload that follows.
    /// </summary>
    /// <param name="opcode">What the frame carries.</param>
    /// <param name="isFinal">Whether the FIN bit is set: the frame ends its message.</param>
    /// <param name="payloadLength">The whole payload's size, which may come in several parts.</param>
    /// <param name="randomSource">Supplies the frame's 4-byte mask.</param>
    /// <returns>The head's bytes, ending with the mask.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="randomSource" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="payloadLength" /> is negative.</exception>
    public static byte[] EncodeHead(WsOpcode opcode, bool isFinal, long payloadLength, IWebSocketRandomSource randomSource)
    {
        ArgumentNullException.ThrowIfNull(randomSource);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadLength);
        int lengthFieldLength = LengthFieldLength(payloadLength);
        int maskOffset = 2 + lengthFieldLength;
        byte[] head = new byte[maskOffset + MaskLength];
        head[0] = (byte)((isFinal ? FinalFragment : 0) | (byte)opcode);
        WriteLength(head, payloadLength, lengthFieldLength);
        randomSource.Fill(head.AsSpan(maskOffset, MaskLength));
        return head;
    }

    /// <summary>
    /// Masks one part of a frame's payload: each byte with the mask byte its position in the whole payload
    /// picks, so parts masked one after another read as the payload masked at once.
    /// </summary>
    /// <param name="part">The part, unmasked.</param>
    /// <param name="mask">The frame's <see cref="MaskLength" />-byte mask, the last bytes of its head.</param>
    /// <param name="offset">Where <paramref name="part" /> starts in the frame's payload.</param>
    /// <returns>The part masked.</returns>
    /// <exception cref="ArgumentException"><paramref name="mask" /> is not <see cref="MaskLength" /> bytes.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset" /> is negative.</exception>
    public static byte[] MaskPart(ReadOnlySpan<byte> part, ReadOnlySpan<byte> mask, long offset)
    {
        if (mask.Length != MaskLength)
        {
            throw new ArgumentException($"A frame's mask is {MaskLength} bytes.", nameof(mask));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        byte[] masked = new byte[part.Length];
        MaskInto(part, mask, offset, masked);
        return masked;
    }

    private static void MaskInto(ReadOnlySpan<byte> part, ReadOnlySpan<byte> mask, long offset, Span<byte> destination)
    {
        int first = (int)(offset % MaskLength);
        for (int index = 0; index < part.Length; index++)
        {
            destination[index] = (byte)(part[index] ^ mask[(first + index) % MaskLength]);
        }
    }

    private static int LengthFieldLength(long payloadLength) => payloadLength switch
    {
        <= Longest7BitLength => 0,
        <= ushort.MaxValue => sizeof(ushort),
        _ => sizeof(ulong),
    };

    private static void WriteLength(byte[] head, long payloadLength, int lengthFieldLength)
    {
        switch (lengthFieldLength)
        {
            case 0:
                head[1] = (byte)(Masked | payloadLength);
                break;
            case sizeof(ushort):
                head[1] = Masked | Announces16BitLength;
                BinaryPrimitives.WriteUInt16BigEndian(head.AsSpan(2), (ushort)payloadLength);
                break;
            default:
                head[1] = Masked | Announces64BitLength;
                BinaryPrimitives.WriteUInt64BigEndian(head.AsSpan(2), (ulong)payloadLength);
                break;
        }
    }
}
