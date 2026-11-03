using System.Buffers.Binary;

namespace Curl.Conformance.Extended.SignalR;

/// <summary>
/// A WebSocket frame (RFC 6455 section 5.2) curl sent the reference hub, its payload unmasked. Curl's frames stay
/// under 64 KiB, so the 64-bit length form is not read, and a frame is read once at least its first four bytes are
/// in (a client frame is masked, so it has at least six).
/// </summary>
/// <param name="IsFinal">Whether FIN is set.</param>
/// <param name="Opcode">The opcode: 0x0 continuation, 0x1 text, 0x2 binary, 0x8 close, 0x9 ping, 0xA pong.</param>
/// <param name="IsMasked">Whether the MASK bit is set.</param>
/// <param name="Payload">The payload, unmasked with the frame's key when it has one.</param>
public sealed record SignalRReferenceFrame(bool IsFinal, int Opcode, bool IsMasked, byte[] Payload)
{
    /// <summary>The text opcode.</summary>
    public const int Text = 0x1;

    /// <summary>The close opcode.</summary>
    public const int Close = 0x8;

    /// <summary>The ping opcode.</summary>
    public const int Ping = 0x9;

    /// <summary>The pong opcode.</summary>
    public const int Pong = 0xA;

    /// <summary>Reads one whole frame from the start of <paramref name="pending" />.</summary>
    /// <param name="pending">The bytes received and not yet read.</param>
    /// <param name="frame">The frame, or <see langword="null" /> when <paramref name="pending" /> holds no whole one yet.</param>
    /// <returns>The number of bytes the frame took; 0 when it is not whole yet.</returns>
    public static int TryRead(ReadOnlySpan<byte> pending, out SignalRReferenceFrame? frame)
    {
        frame = null;
        if (pending.Length < 4)
        {
            return 0;
        }

        bool isMasked = (pending[1] & 0x80) != 0;
        (int lengthEnd, int length) = ReadPayloadLength(pending);
        int payloadStart = lengthEnd + (isMasked ? 4 : 0);
        if (pending.Length < payloadStart + length)
        {
            return 0;
        }

        byte[] payload = pending.Slice(payloadStart, length).ToArray();
        if (isMasked)
        {
            Unmask(payload, pending.Slice(lengthEnd, 4));
        }

        frame = new SignalRReferenceFrame((pending[0] & 0x80) != 0, pending[0] & 0x0F, isMasked, payload);
        return payloadStart + length;
    }

    private static (int LengthEnd, int Length) ReadPayloadLength(ReadOnlySpan<byte> pending)
    {
        int length = pending[1] & 0x7F;
        return length == 126 ? (4, BinaryPrimitives.ReadUInt16BigEndian(pending[2..])) : (2, length);
    }

    private static void Unmask(byte[] payload, ReadOnlySpan<byte> key)
    {
        for (int index = 0; index < payload.Length; index++)
        {
            payload[index] ^= key[index % 4];
        }
    }

    /// <summary>Writes a server frame, unmasked (RFC 6455 section 5.1), with a payload under 64 KiB.</summary>
    /// <param name="firstByte">FIN, the reserved bits and the opcode, such as 0x81 for a final text frame.</param>
    /// <param name="payload">The payload.</param>
    /// <returns>The frame's bytes.</returns>
    public static byte[] Write(int firstByte, ReadOnlySpan<byte> payload)
    {
        byte[] length = payload.Length < 126 ? [(byte)payload.Length] : [126, (byte)(payload.Length >> 8), (byte)payload.Length];
        return [(byte)firstByte, .. length, .. payload];
    }
}
