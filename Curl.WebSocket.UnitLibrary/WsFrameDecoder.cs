using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;

namespace Curl.WebSocket;

/// <summary>
/// Reads RFC 6455 server frames from bytes as they arrive, keeping its place between calls, so a
/// frame split across reads and several frames in one read decode the same (ADR-0131).
/// </summary>
/// <remarks>
/// Payloads are passed on as they arrive, not held until the frame ends, because curl writes a
/// partial frame's payload before the connection closes (measured, BL-581). Only a ping's
/// payload is held, to be echoed in a pong once the ping is complete. Each violation stops decoding with
/// curl 8.21.0's message (curl then exits 56), checked in curl's order: reserved bits and opcode on the
/// first byte, the mask bit on the second, then the length.
/// </remarks>
/// <param name="observer">
/// Told of each frame opened, each run of payload passed on and each close frame's payload, for the
/// protocol's diagnostic log and <c>--trace-config ws</c> lines; <see langword="null" /> tells no one.
/// </param>
/// <param name="recordsRuns">
/// Whether each run of payload, ping payloads included, is kept with its frame's metadata for
/// <see cref="TakeRuns" /> (<see cref="WsFrameMetadataDecoder" />, BL-2471).
/// </param>
public sealed class WsFrameDecoder(IWsFrameDecodeObserver? observer = null, bool recordsRuns = false)
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

    private readonly IWsFrameDecodeObserver observer = observer ?? NoWsFrameDecodeObserver.Instance;

    // A ping's payload, echoed in a pong, or a close frame's, whose code is logged.
    private readonly ArrayBufferWriter<byte> controlPayload = new(Longest7BitLength);

    private readonly List<WsFrameRun>? runs = recordsRuns ? [] : null;

    private int headCount;

    private long payloadLength;

    private long payloadRemaining;

    private bool isFrameOpen;

    private WsOpcode opcode;

    private bool isMessageOngoing;

    private byte[]? lastPing;

    /// <summary>Decodes the next run of received bytes.</summary>
    /// <param name="received">The bytes, continuing from where the previous call ended.</param>
    /// <returns>The payload to write, the last completed ping, and any violation.</returns>
    public WsDecodedBytes Decode(ReadOnlySpan<byte> received)
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
        catch (WsFrameViolationException violation)
        {
            return new WsDecodedBytes(payload.WrittenSpan.ToArray(), null, violation.Message);
        }

        return new WsDecodedBytes(payload.WrittenSpan.ToArray(), lastPing, null);
    }

    /// <summary>
    /// Gives the runs of payload decoded since the last call, with their frames' metadata, and
    /// forgets them; only for a decoder made to record runs.
    /// </summary>
    public WsFrameRun[] TakeRuns()
    {
        WsFrameRun[] taken = [.. runs!];
        runs!.Clear();
        return taken;
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
            OpenFrame(isLastByteOfRead: received.Length == 1);
        }

        return received[1..];
    }

    private ReadOnlySpan<byte> ConsumePayload(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> payload)
    {
        int length = (int)Math.Min(received.Length, payloadRemaining);
        ReadOnlySpan<byte> chunk = received[..length];
        (opcode == WsOpcode.Ping ? controlPayload : payload).Write(chunk);
        if (opcode == WsOpcode.Close)
        {
            controlPayload.Write(chunk);
        }

        runs?.Add(new WsFrameRun(opcode, IsFinal(), payloadLength - payloadRemaining, payloadRemaining - length, chunk.ToArray()));
        payloadRemaining -= length;
        observer.PayloadPassed(opcode, IsFinal(), length, payloadLength - payloadRemaining, payloadLength);
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

    /// <summary>
    /// Opens the frame whose head is complete. When its head ends the read and a payload is still
    /// to come, curl 8.21.0 already writes the frame as passing with none of it passed (BL-1164).
    /// </summary>
    private void OpenFrame(bool isLastByteOfRead)
    {
        opcode = (WsOpcode)(head[0] & OpcodeBits);
        payloadLength = PayloadLength();
        payloadRemaining = payloadLength;
        observer.FrameOpened(opcode, IsFinal(), payloadLength);
        headCount = 0;
        isFrameOpen = true;
        controlPayload.ResetWrittenCount();
        if (payloadRemaining == 0)
        {
            runs?.Add(new WsFrameRun(opcode, IsFinal(), 0, 0, ReadOnlyMemory<byte>.Empty));
            CloseFrame();
        }
        else if (isLastByteOfRead)
        {
            observer.HeadEndedRead(opcode, IsFinal(), payloadLength);
        }
    }

    private bool IsFinal() => (head[0] & FinalFragment) != 0;

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
            lastPing = controlPayload.WrittenSpan.ToArray();
        }
        else if (opcode == WsOpcode.Close)
        {
            observer.CloseReceived(controlPayload.WrittenSpan);
        }
    }

    /// <summary>Names an opcode as curl's messages do: <c>TEXT</c>, <c>BINARY</c>, <c>CLOSE</c>, <c>PING</c>, <c>PONG</c>.</summary>
    private static string Name(WsOpcode frameOpcode) => frameOpcode.ToString().ToUpperInvariant();

    private static WsFrameViolationException Violation(string message) =>
        new("[WS] " + message);
}
