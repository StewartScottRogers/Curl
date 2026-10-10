using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// One client connection to a <see cref="MqttServerConnector"/>, answering as mqttd's <c>mqttit</c>
/// loop does. Every complete packet the client writes is answered at once, however the writes
/// split it; a read with nothing waiting waits for the client's next write, as mqttd blocks
/// reading a packet, and returns 0 once the server has closed.
/// </summary>
/// <remarks>
/// A CONNECT with the MQTT 3.1.1 preamble and consistent lengths is answered with CONNACK; a
/// SUBSCRIBE with SUBACK, a PUBLISH of the <c>&lt;reply&gt;&lt;data&gt;</c> to its topic and
/// DISCONNECT (with no data part, only a PUBLISH of mqttd's default payload and DISCONNECT); a
/// PUBLISH is followed by reading two bytes as the client's DISCONNECT, then a close. Any other
/// packet, a failed check or a short PUBLISH closes the connection. Each packet read or written is
/// logged as <c>client|server TYPE remaining-length-in-hex hex-dump</c>, as mqttd's
/// <c>logprotocol</c> does: a client packet's dump is its body after the fixed header, a server
/// packet's dump the whole packet, and the client's DISCONNECT the two bytes read.
/// </remarks>
internal sealed class MqttServerConnection : IConnection
{
    private const int ClientIdOffset = 12;

    // mqttd's MAX_CLIENT_ID_LENGTH.
    private const int MaximumClientIdLength = 32;

    private static readonly byte[] Preamble = [0x00, 0x04, (byte)'M', (byte)'Q', (byte)'T', (byte)'T', 0x04];

    private static readonly byte[] DefaultPayload = Encoding.Latin1.GetBytes("this is random payload yes yes it is");

    private readonly MqttServerConfiguration configuration;

    private readonly SwsServerRecording recording;

    private readonly Lock gate = new();

    private readonly List<byte> unreadReplyBytes = [];

    private readonly List<byte> unreadClientBytes = [];

    private TaskCompletionSource nextWrite = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private bool closed;

    private bool awaitingDisconnect;

    public MqttServerConnection(MqttServerConfiguration configuration, SwsServerRecording recording)
    {
        this.configuration = configuration;
        this.recording = recording;
    }

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint { get; init; }

    public EndPoint? LocalEndPoint { get; init; }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        Task written;
        lock (gate)
        {
            if (unreadReplyBytes.Count > 0 || closed)
            {
                int count = Math.Min(buffer.Length, unreadReplyBytes.Count);
                CollectionsMarshal.AsSpan(unreadReplyBytes)[..count].CopyTo(buffer.Span);
                unreadReplyBytes.RemoveRange(0, count);
                return count;
            }

            written = nextWrite.Task;
        }

        await written.WaitAsync(cancellationToken);
        return await ReadAsync(buffer, cancellationToken);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        TaskCompletionSource written;
        lock (gate)
        {
            if (!closed)
            {
                unreadClientBytes.AddRange(buffer.Span);
                while (!closed && AnswerNextPacket())
                {
                }
            }

            written = nextWrite;
            nextWrite = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        written.TrySetResult();
        return ValueTask.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    // A client that closes while mqttd waits for its DISCONNECT has that read return what came,
    // which mqttd logs all the same.
    public ValueTask DisposeAsync()
    {
        lock (gate)
        {
            if (awaitingDisconnect)
            {
                LogClientDisconnect();
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Decodes an MQTT remaining length, as mqttd's <c>decode_length</c> does.</summary>
    /// <param name="encoded">The bytes after the packet type, at least the whole encoding.</param>
    /// <param name="encodedLength">How many bytes the encoding takes, or 0 when <paramref name="encoded"/> does not hold it all yet.</param>
    /// <returns>The remaining length.</returns>
    internal static int DecodeLength(ReadOnlySpan<byte> encoded, out int encodedLength)
    {
        int length = 0;
        int multiplier = 1;
        for (int index = 0; index < encoded.Length; index++)
        {
            length += (encoded[index] & 0x7F) * multiplier;
            multiplier *= 128;
            if ((encoded[index] & 0x80) == 0)
            {
                encodedLength = index + 1;
                return length;
            }
        }

        encodedLength = 0;
        return 0;
    }

    /// <summary>Encodes an MQTT remaining length.</summary>
    /// <param name="length">The remaining length.</param>
    /// <returns>Its encoding.</returns>
    internal static byte[] EncodeLength(int length)
    {
        List<byte> encoded = [];
        do
        {
            byte digit = (byte)(length % 128);
            length /= 128;
            encoded.Add(length > 0 ? (byte)(digit | 0x80) : digit);
        }
        while (length > 0);
        return [.. encoded];
    }

    // Answers the first whole packet waiting; false when none is whole yet.
    private bool AnswerNextPacket()
    {
        if (awaitingDisconnect)
        {
            if (unreadClientBytes.Count < 2)
            {
                return false;
            }

            LogClientDisconnect();
            return true;
        }

        int remainingLength = DecodeLength(CollectionsMarshal.AsSpan(unreadClientBytes)[Math.Min(1, unreadClientBytes.Count)..], out int encodedLength);
        int headerLength = 1 + encodedLength;
        if (encodedLength == 0 || unreadClientBytes.Count - headerLength < remainingLength)
        {
            return false;
        }

        byte type = unreadClientBytes[0];
        byte[] body = unreadClientBytes.GetRange(headerLength, remainingLength).ToArray();
        unreadClientBytes.RemoveRange(0, headerLength + remainingLength);
        Answer(type, body);
        return true;
    }

    private void Answer(byte type, byte[] body)
    {
        switch (type & 0xF0)
        {
            case 0x10:
                LogPacket("client", "CONNECT", body.Length, body);
                AnswerConnect(body);
                break;
            case 0x80:
                LogPacket("client", "SUBSCRIBE", body.Length, body);
                AnswerSubscribe(body);
                break;
            case 0x30:
                LogPacket("client", "PUBLISH", body.Length, body);
                awaitingDisconnect = true;
                break;
            default:
                closed = true;
                break;
        }
    }

    private void LogClientDisconnect()
    {
        int count = Math.Min(2, unreadClientBytes.Count);
        LogPacket("client", "DISCONNECT", 0, unreadClientBytes.GetRange(0, count).ToArray());
        unreadClientBytes.Clear();
        awaitingDisconnect = false;
        closed = true;
    }

    private void AnswerConnect(byte[] body)
    {
        if (!IsAcceptedConnect(body))
        {
            closed = true;
        }
        else if (configuration.PingrespAsConnack)
        {
            Send("PINGRESP-as-CONNACK", 2, [0xD0, 0x02, 0x00, 0x00]);
        }
        else
        {
            Send("CONNACK", configuration.ConnackRemainingLength, [0x20, configuration.ConnackRemainingLength, 0x00, configuration.ConnackReturnCode]);
        }
    }

    // mqttd's checks: the preamble, a payload as long as the client id, user name and password
    // the flags announce, and a client id shorter than its buffer.
    private static bool IsAcceptedConnect(byte[] body)
    {
        if (body.Length < ClientIdOffset || !body.AsSpan(0, Preamble.Length).SequenceEqual(Preamble))
        {
            return false;
        }

        int clientIdLength = (body[10] << 8) | body[11];
        int payloadLength = clientIdLength;
        payloadLength += FlaggedFieldLength(body, 0x80, ClientIdOffset + payloadLength);
        payloadLength += FlaggedFieldLength(body, 0x40, ClientIdOffset + payloadLength);
        return payloadLength == body.Length - ClientIdOffset && clientIdLength + 1 <= MaximumClientIdLength;
    }

    // A field's two length bytes and its length when the flag is set; a length past the body
    // makes the payload length mismatch.
    private static int FlaggedFieldLength(byte[] body, byte flag, int start) =>
        (body[7] & flag) == 0 ? 0
        : start + 2 <= body.Length ? 2 + ((body[start] << 8) | body[start + 1])
        : body.Length;

    private void AnswerSubscribe(byte[] body)
    {
        int topicLength = body.Length < 6 ? -1 : (body[2] << 8) | body[3];
        if (topicLength != body.Length - 5)
        {
            closed = true;
            return;
        }

        byte[] packetId = body[..2];
        byte[] topic = body[4..(4 + topicLength)];
        if (configuration.Payload is not { } payload)
        {
            _ = Publish(topic, DefaultPayload);
        }
        else if (!(configuration.PublishBeforeSuback
            ? Publish(topic, payload) && Suback(packetId)
            : Suback(packetId) && Publish(topic, payload)))
        {
            return;
        }

        if (configuration.MalformedDisconnect)
        {
            Send("DISCONNECT-malformed", 2, [0xE0, 0x02, 0x00, 0x00]);
        }
        else
        {
            Send("DISCONNECT", 0, [0xE0, 0x00]);
        }
    }

    private bool Suback(byte[] packetId)
    {
        Send("SUBACK", 3, [0x90, 0x03, packetId[0], packetId[1], 0x00]);
        return true;
    }

    // mqttd sends a short PUBLISH two bytes short and then gives up on the connection.
    private bool Publish(byte[] topic, byte[] payload)
    {
        int remainingLength = 2 + topic.Length + payload.Length;
        byte[] encodedLength = configuration.ExcessiveRemaining ? [0xFF, 0xFF, 0xFF, 0x80] : EncodeLength(remainingLength);
        byte[] packet = [0x30, .. encodedLength, (byte)(topic.Length >> 8), (byte)topic.Length, .. topic, .. payload];
        int sent = configuration.ShortPublish ? packet.Length - 2 : packet.Length;
        Send("PUBLISH", remainingLength, packet[..sent]);
        closed = configuration.ShortPublish;
        return !closed;
    }

    private void Send(string type, int remainingLength, byte[] packet)
    {
        unreadReplyBytes.AddRange(packet);
        LogPacket("server", type, remainingLength, packet);
    }

    private void LogPacket(string direction, string type, int remainingLength, byte[] dump) =>
        recording.Record(Encoding.Latin1.GetBytes(
            $"{direction} {type} {remainingLength:x} {Convert.ToHexStringLower(dump)}\n"));
}
