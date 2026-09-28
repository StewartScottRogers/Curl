using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws;

/// <summary>
/// Reads RFC 6455 server frames from bytes as they arrive, keeping its place between calls, so a
/// frame split across reads and several frames in one read decode the same (ADR-0131).
/// </summary>
/// <remarks>
/// Payloads are passed on as they arrive, not held until the frame ends, because curl writes a
/// partial frame's payload before the connection closes (measured, BL-581). Only a ping's
/// payload is held, to be echoed in a pong once the ping is complete. Each violation fails with
/// exit 56 and curl 8.21.0's message, checked in curl's order: reserved bits and opcode on the
/// first byte, the mask bit on the second, then the length.
/// </remarks>
internal sealed class WsFrameDecoder
{
    private const byte FinalFragment = 0x80;

    private const byte ReservedBits = 0x70;

    private const byte OpcodeBits = 0x0F;

    private const byte Masked = 0x80;

    private const byte LengthBits = 0x7F;

    private const byte Announces16BitLength = 126;

    private const byte Announces64BitLength = 127;

    private const int Longest7BitLength = 125;

    private const int LongestHead = 10;

    private readonly byte[] head = new byte[LongestHead];

    private readonly ArrayBufferWriter<byte> pingPayload = new(Longest7BitLength);

    private int headCount;

    private long payloadRemaining;

    private bool isFrameOpen;

    private WsOpcode opcode;

    private bool isMessageOngoing;

    private byte[]? lastPing;

    /// <summary>Decodes the next run of received bytes.</summary>
    /// <param name="received">The bytes, continuing from where the previous call ended.</param>
    /// <returns>The payload to write, the last completed ping, and any violation.</returns>
    internal WsDecodedBytes Decode(ReadOnlySpan<byte> received)
    {
        ArrayBufferWriter<byte> payload = new();
        lastPing = null;
        try
        {
            while (!received.IsEmpty)
            {
                received = isFrameOpen
                    ? ConsumePayload(received, payload)
                    : ConsumeHeadByte(received);
            }
        }
        catch (WsTransferException failure)
        {
            return new WsDecodedBytes(payload.WrittenSpan.ToArray(), null, failure);
        }

        return new WsDecodedBytes(payload.WrittenSpan.ToArray(), lastPing, null);
    }

    private ReadOnlySpan<byte> ConsumeHeadByte(ReadOnlySpan<byte> received)
    {
        head[headCount++] = received[0];
        if (headCount == 1)
        {
            CheckFirstByte(received[0]);
        }
        else if (headCount == 2)
        {
            CheckSecondByte(received[0]);
        }

        if (headCount == HeadLength())
        {
            OpenFrame();
        }

        return received[1..];
    }

    private ReadOnlySpan<byte> ConsumePayload(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> payload)
    {
        int length = (int)Math.Min(received.Length, payloadRemaining);
        (opcode == WsOpcode.Ping ? pingPayload : payload).Write(received[..length]);
        payloadRemaining -= length;
        if (payloadRemaining == 0)
        {
            CloseFrame();
        }

        return received[length..];
    }

    private void CheckFirstByte(byte first)
    {
        if ((first & ReservedBits) != 0)
        {
            throw Violation(string.Create(CultureInfo.InvariantCulture, $"invalid reserved bits: {first:x2}"));
        }

        var frameOpcode = (WsOpcode)(first & OpcodeBits);
        bool isFinal = (first & FinalFragment) != 0;
        if (frameOpcode == WsOpcode.Continuation)
        {
            ContinueMessage(isFinal);
        }
        else if (IsMessageStart(frameOpcode))
        {
            StartMessage(frameOpcode, isFinal);
        }
        else if (IsControl(frameOpcode))
        {
            CheckControlFrameIsWhole(frameOpcode, isFinal);
        }
        else
        {
            throw Violation(string.Create(CultureInfo.InvariantCulture, $"invalid opcode: {first:x2}"));
        }
    }

    private static bool IsMessageStart(WsOpcode frameOpcode) => frameOpcode is WsOpcode.Text or WsOpcode.Binary;

    private static bool IsControl(WsOpcode frameOpcode) => frameOpcode is WsOpcode.Close or WsOpcode.Ping or WsOpcode.Pong;

    private void ContinueMessage(bool isFinal)
    {
        if (!isMessageOngoing)
        {
            throw Violation("no ongoing fragmented message to resume");
        }

        isMessageOngoing = !isFinal;
    }

    private void StartMessage(WsOpcode frameOpcode, bool isFinal)
    {
        if (isMessageOngoing)
        {
            throw Violation($"fragmented message interrupted by new {Name(frameOpcode)} msg");
        }

        isMessageOngoing = !isFinal;
    }

    private static void CheckControlFrameIsWhole(WsOpcode frameOpcode, bool isFinal)
    {
        if (!isFinal)
        {
            throw Violation($"invalid fragmented {Name(frameOpcode)} frame");
        }
    }

    private void CheckSecondByte(byte second)
    {
        if ((second & Masked) != 0)
        {
            throw Violation("masked input frame");
        }

        var frameOpcode = (WsOpcode)(head[0] & OpcodeBits);
        if (frameOpcode >= WsOpcode.Close && (second & LengthBits) > Longest7BitLength)
        {
            throw Violation($"received {Name(frameOpcode)} frame is too big");
        }
    }

    private int HeadLength() => headCount < 2
        ? 2
        : (head[1] & LengthBits) switch
        {
            Announces16BitLength => 2 + sizeof(ushort),
            Announces64BitLength => 2 + sizeof(ulong),
            _ => 2,
        };

    private void OpenFrame()
    {
        opcode = (WsOpcode)(head[0] & OpcodeBits);
        payloadRemaining = PayloadLength();
        headCount = 0;
        isFrameOpen = true;
        pingPayload.ResetWrittenCount();
        if (payloadRemaining == 0)
        {
            CloseFrame();
        }
    }

    private long PayloadLength()
    {
        switch (head[1] & LengthBits)
        {
            case Announces16BitLength:
                return BinaryPrimitives.ReadUInt16BigEndian(head.AsSpan(2));
            case Announces64BitLength:
                ulong length = BinaryPrimitives.ReadUInt64BigEndian(head.AsSpan(2));
                if (length > long.MaxValue)
                {
                    throw Violation("frame length longer than 63 bits not supported");
                }

                return (long)length;
            default:
                return head[1] & LengthBits;
        }
    }

    private void CloseFrame()
    {
        isFrameOpen = false;
        if (opcode == WsOpcode.Ping)
        {
            lastPing = pingPayload.WrittenSpan.ToArray();
        }
    }

    /// <summary>Names an opcode as curl's messages do: <c>TEXT</c>, <c>BINARY</c>, <c>CLOSE</c>, <c>PING</c>, <c>PONG</c>.</summary>
    private static string Name(WsOpcode frameOpcode) => frameOpcode.ToString().ToUpperInvariant();

    private static WsTransferException Violation(string message) =>
        new(CurlExitCode.RecvError, "[WS] " + message);
}
