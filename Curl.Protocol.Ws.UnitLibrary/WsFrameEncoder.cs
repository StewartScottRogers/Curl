using System.Buffers.Binary;

namespace Curl.Protocol.Ws;

/// <summary>
/// Writes one client frame as RFC 6455 requires and curl 8.21.0 sends it: the FIN bit set, the
/// mask bit set, the shortest of the 7-, 16- and 64-bit length forms, then a 4-byte mask drawn
/// from the injected random source and the payload masked with it (ADR-0128, ADR-0131).
/// </summary>
internal static class WsFrameEncoder
{
    /// <summary>The longest payload the 7-bit length form carries.</summary>
    private const int Longest7BitLength = 125;

    /// <summary>The 7-bit length value that announces a 16-bit length.</summary>
    private const byte Announces16BitLength = 126;

    /// <summary>The 7-bit length value that announces a 64-bit length.</summary>
    private const byte Announces64BitLength = 127;

    private const byte FinalFragment = 0x80;

    private const byte Masked = 0x80;

    private const int MaskLength = 4;

    /// <summary>Writes a masked, unfragmented frame.</summary>
    /// <param name="opcode">What the frame carries.</param>
    /// <param name="payload">The payload, unmasked.</param>
    /// <param name="randomSource">Supplies the frame's 4-byte mask.</param>
    /// <returns>The frame's bytes, ready to send.</returns>
    internal static byte[] Encode(WsOpcode opcode, ReadOnlySpan<byte> payload, IWebSocketRandomSource randomSource)
    {
        int lengthFieldLength = LengthFieldLength(payload.Length);
        int maskOffset = 2 + lengthFieldLength;
        int payloadOffset = maskOffset + MaskLength;
        byte[] frame = new byte[payloadOffset + payload.Length];
        frame[0] = (byte)(FinalFragment | (byte)opcode);
        WriteLength(frame, payload.Length, lengthFieldLength);
        Span<byte> mask = frame.AsSpan(maskOffset, MaskLength);
        randomSource.Fill(mask);
        for (int index = 0; index < payload.Length; index++)
        {
            frame[payloadOffset + index] = (byte)(payload[index] ^ mask[index % MaskLength]);
        }

        return frame;
    }

    private static int LengthFieldLength(int payloadLength) => payloadLength switch
    {
        <= Longest7BitLength => 0,
        <= ushort.MaxValue => sizeof(ushort),
        _ => sizeof(ulong),
    };

    private static void WriteLength(byte[] frame, int payloadLength, int lengthFieldLength)
    {
        switch (lengthFieldLength)
        {
            case 0:
                frame[1] = (byte)(Masked | payloadLength);
                break;
            case sizeof(ushort):
                frame[1] = Masked | Announces16BitLength;
                BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2), (ushort)payloadLength);
                break;
            default:
                frame[1] = Masked | Announces64BitLength;
                BinaryPrimitives.WriteUInt64BigEndian(frame.AsSpan(2), (ulong)payloadLength);
                break;
        }
    }
}
