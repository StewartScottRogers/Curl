using System.Security.Cryptography;
using System.Text;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.Mqtt5.Transports;

/// <summary>
/// The WebSocket front end of the MQTT 5 reference broker for <c>mqtt5+ws://</c> conformance (BL-4486), written
/// from RFC 6455 and OASIS MQTT Version 5.0 chapter 6. It reads curl's opening handshake request, answers it with
/// the reply its <c>upgradeReply</c> writes for the request's key, then reads RFC 6455 frames, records each, joins
/// the binary and continuation payloads into one MQTT byte stream, and answers each whole MQTT packet with the
/// writes its <c>respond</c> returns (each already a frame or several; see <see cref="Mqtt5WebSocketFrame.Encode" />).
/// </summary>
/// <param name="upgradeReply">Writes the handshake reply head for a key; <see cref="SuccessReply" /> when null.</param>
/// <param name="respond">The writes that answer an MQTT packet; <see cref="ConnAckInOneFrame" /> when null.</param>
public sealed class Mqtt5WebSocketReferenceServer(Func<string, string>? upgradeReply = null, Func<Mqtt5WirePacket, IReadOnlyList<byte[]>>? respond = null)
    : IReferenceServer
{
    private const string KeyHeader = "Sec-WebSocket-Key";

    private static readonly byte[] HeadEnd = "\r\n\r\n"u8.ToArray();

    private readonly List<Mqtt5WebSocketFrame> frames = [];

    private readonly List<Mqtt5WirePacket> packets = [];

    private readonly List<byte> mqttBytes = [];

    private readonly Func<Mqtt5WirePacket, IReadOnlyList<byte[]>> answer = respond ?? ConnAckInOneFrame;

    /// <summary>Gets curl's opening handshake request head, without its closing blank line; null until it is read whole.</summary>
    public string? UpgradeRequest { get; private set; }

    /// <summary>Gets every frame curl sent after the handshake, in order.</summary>
    public IReadOnlyList<Mqtt5WebSocketFrame> ReceivedFrames => frames;

    /// <summary>Gets every MQTT packet curl's binary frames carried, in order.</summary>
    public IReadOnlyList<Mqtt5WirePacket> ReceivedPackets => packets;

    /// <summary>Gets the Sec-WebSocket-Accept value RFC 6455 section 4.2.2 derives from a key.</summary>
    /// <param name="key">The request's Sec-WebSocket-Key.</param>
    /// <returns>The base64 SHA-1 of the key and the RFC's GUID.</returns>
    public static string AcceptFor(string key) =>
        Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));

    /// <summary>Gets a 101 reply that accepts the key and selects the <c>mqtt</c> subprotocol.</summary>
    /// <param name="key">The request's Sec-WebSocket-Key.</param>
    /// <returns>The reply head, ending in its blank line.</returns>
    public static string SuccessReply(string key) =>
        "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n" +
        $"Sec-WebSocket-Accept: {AcceptFor(key)}\r\nSec-WebSocket-Protocol: mqtt\r\n\r\n";

    /// <summary>Answers CONNECT with a success CONNACK in one binary frame, and nothing else.</summary>
    /// <param name="packet">The packet curl sent.</param>
    /// <returns>The writes.</returns>
    public static IReadOnlyList<byte[]> ConnAckInOneFrame(Mqtt5WirePacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        return packet.PacketType == Mqtt5WirePacket.Connect
            ? [Mqtt5WebSocketFrame.Encode(Mqtt5WebSocketFrame.Binary, Mqtt5ReferenceBroker.SuccessConnAck)]
            : [];
    }

    /// <summary>Gets the value of a request head's Sec-WebSocket-Key header field.</summary>
    /// <param name="requestHead">The request head.</param>
    /// <returns>The key, trimmed; empty when the head has none.</returns>
    public static string KeyOf(string requestHead)
    {
        ArgumentNullException.ThrowIfNull(requestHead);
        return requestHead.Split("\r\n")
            .Select(static line => line.Split(':', 2))
            .Where(static field => field.Length == 2 && field[0].Equals(KeyHeader, StringComparison.OrdinalIgnoreCase))
            .Select(static field => field[1].Trim())
            .FirstOrDefault(string.Empty);
    }

    /// <summary>Takes the first whole MQTT packet off the front of <paramref name="stream" />.</summary>
    /// <param name="stream">MQTT bytes not yet split into packets; the packet's bytes are removed from it.</param>
    /// <returns>The packet, or null while <paramref name="stream" /> holds less than a whole one.</returns>
    public static Mqtt5WirePacket? TakePacket(List<byte> stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        int remainingLength = 0;
        int index = 1;
        int multiplier = 1;
        do
        {
            if (index >= stream.Count)
            {
                return null;
            }

            remainingLength += (stream[index] & 0x7F) * multiplier;
            multiplier *= 128;
        }
        while ((stream[index++] & 0x80) != 0);

        if (stream.Count - index < remainingLength)
        {
            return null;
        }

        Mqtt5WirePacket packet = new(stream[0], [.. stream.GetRange(index, remainingLength)]);
        stream.RemoveRange(0, index + remainingLength);
        return packet;
    }

    /// <inheritdoc />
    public async Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        List<byte> pending = [];
        byte[] chunk = new byte[4096];
        int read = 1;
        while (UpgradeRequest is null && read > 0)
        {
            read = await connection.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            pending.AddRange(chunk.AsSpan(0, read));
            await AnswerHeadAsync(connection, pending, cancellationToken).ConfigureAwait(false);
        }

        while (read > 0)
        {
            while (Mqtt5WebSocketFrame.TakeFrom(pending) is { } frame)
            {
                await AcceptFrameAsync(connection, frame, cancellationToken).ConfigureAwait(false);
            }

            read = await connection.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            pending.AddRange(chunk.AsSpan(0, read));
        }
    }

    private async Task AnswerHeadAsync(ReferenceConnection connection, List<byte> pending, CancellationToken cancellationToken)
    {
        int headEnd = pending.ToArray().AsSpan().IndexOf(HeadEnd);
        if (headEnd < 0)
        {
            return;
        }

        UpgradeRequest = Encoding.Latin1.GetString([.. pending.GetRange(0, headEnd)]);
        pending.RemoveRange(0, headEnd + HeadEnd.Length);
        string reply = (upgradeReply ?? SuccessReply)(KeyOf(UpgradeRequest));
        await connection.WriteAsync(Encoding.Latin1.GetBytes(reply), cancellationToken).ConfigureAwait(false);
    }

    private async Task AcceptFrameAsync(ReferenceConnection connection, Mqtt5WebSocketFrame frame, CancellationToken cancellationToken)
    {
        foreach (byte[] write in AnswersTo(frame))
        {
            await connection.WriteAsync(write, cancellationToken).ConfigureAwait(false);
        }
    }

    // Records the frame and every MQTT packet it completes, and gets the writes that answer those packets.
    private List<byte[]> AnswersTo(Mqtt5WebSocketFrame frame)
    {
        Collect(frame);
        List<byte[]> writes = [];
        while (TakePacket(mqttBytes) is { } packet)
        {
            packets.Add(packet);
            writes.AddRange(answer(packet));
        }

        return writes;
    }

    // Records the frame and adds a data frame's payload to the MQTT byte stream.
    private void Collect(Mqtt5WebSocketFrame frame)
    {
        frames.Add(frame);
        if (frame.Opcode is Mqtt5WebSocketFrame.Binary or Mqtt5WebSocketFrame.Continuation)
        {
            mqttBytes.AddRange(frame.Payload);
        }
    }
}
