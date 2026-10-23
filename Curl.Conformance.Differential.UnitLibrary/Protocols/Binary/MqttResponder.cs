using System.Buffers.Binary;
using System.Globalization;
using Curl.Conformance.Differential.Http.Core;
using Curl.Conformance.Differential.Protocols.Core;

namespace Curl.Conformance.Differential.Protocols.Binary;

/// <summary>
/// Answers an MQTT connection the way proto2_servers.py's <c>Mqtt</c> class does (port 18450): logs every
/// packet's type, flags and body, answers CONNECT with a CONNACK and SUBSCRIBE with a SUBACK and one PUBLISH of
/// <c>payload</c> to the subscribed topic, and ends on DISCONNECT. Where the Python handler crashed (a length
/// cut short, a SUBSCRIBE too short to hold a topic length, a topic too long for the PUBLISH length byte) the
/// connection ends, after the SUBACK where Python had already sent it.
/// </summary>
public sealed class MqttResponder(ProtocolWireLog log)
{
    private const int ConnectType = 1;
    private const int SubscribeType = 8;
    private const int DisconnectType = 14;
    private const int PublishOverhead = 9;

    /// <summary>Serves one connection until DISCONNECT, a crash-equivalent packet, or the client hangs up.</summary>
    public async Task RespondAsync(Stream connection, CancellationToken cancellationToken)
    {
        var reader = new DifferentialHttpLineReader(connection);
        while (true)
        {
            var first = await reader.ReadAsync(1, cancellationToken);
            if (first.Length == 0 || await ReadRemainingLengthAsync(reader, cancellationToken) is not { } length)
            {
                return;
            }

            var body = await reader.ReadAsync(length, cancellationToken);
            var type = first[0] >> 4;
            log.Add(string.Create(CultureInfo.InvariantCulture, $"type={type} flags={first[0] & 15} body={PythonLiteral.FormatBytes(body)}"));
            if (type == DisconnectType || !await ReplyAsync(connection, type, body, cancellationToken))
            {
                return;
            }
        }
    }

    private static async Task<int?> ReadRemainingLengthAsync(DifferentialHttpLineReader reader, CancellationToken cancellationToken)
    {
        var multiplier = 1;
        var value = 0;
        while (true)
        {
            var next = await reader.ReadAsync(1, cancellationToken);
            if (next.Length == 0)
            {
                return null;
            }

            value += (next[0] & 127) * multiplier;
            multiplier *= 128;
            if ((next[0] & 128) == 0)
            {
                return value;
            }
        }
    }

    private static async Task<bool> ReplyAsync(Stream connection, int type, byte[] body, CancellationToken cancellationToken)
    {
        switch (type)
        {
            case ConnectType:
                await SendAsync(connection, [0x20, 0x02, 0x00, 0x00], cancellationToken);
                return true;
            case SubscribeType:
                return await SubscribeAsync(connection, body, cancellationToken);
            default:
                return true;
        }
    }

    private static async Task<bool> SubscribeAsync(Stream connection, byte[] body, CancellationToken cancellationToken)
    {
        await SendAsync(connection, [0x90, 0x03, .. body.Take(2), 0x00], cancellationToken);
        if (body.Length < 4)
        {
            return false;
        }

        var topic = body.Skip(4).Take(BinaryPrimitives.ReadUInt16BigEndian(body.AsSpan(2, 2))).ToArray();
        if (topic.Length + PublishOverhead > byte.MaxValue)
        {
            return false;
        }

        var lengthPrefix = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(lengthPrefix, (ushort)topic.Length);
        await SendAsync(connection, [0x30, (byte)(topic.Length + PublishOverhead), .. lengthPrefix, .. topic, .. "payload"u8], cancellationToken);
        return true;
    }

    private static async Task SendAsync(Stream connection, byte[] bytes, CancellationToken cancellationToken)
    {
        await connection.WriteAsync(bytes, cancellationToken);
        await connection.FlushAsync(cancellationToken);
    }
}
