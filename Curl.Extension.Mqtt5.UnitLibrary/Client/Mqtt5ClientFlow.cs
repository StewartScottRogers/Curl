using Curl.Extension.Mqtt5.Packets;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Mqtt5.Client;

/// <summary>
/// curl's <c>mqtt://</c> flow in MQTT 5 over any byte transport: CONNECT and CONNACK, then with a body PUBLISH and
/// DISCONNECT, otherwise SUBSCRIBE, SUBACK and each PUBLISH payload written to the output until the server sends
/// DISCONNECT. A failure reason code ends the transfer as ADR-1049 maps it.
/// </summary>
internal sealed class Mqtt5ClientFlow(IConnection transport, Stream output, ITransferProgress progress, CancellationToken cancellationToken)
{
    /// <summary>The packet identifier of the one SUBSCRIBE the flow sends.</summary>
    internal const ushort SubscribePacketIdentifier = 1;

    private readonly Mqtt5PacketChannel channel = new(transport, cancellationToken);

    /// <summary>The payload bytes written to the output so far.</summary>
    internal long BytesWritten { get; private set; }

    /// <summary>Runs the flow: publishes <paramref name="payload"/> to <paramref name="topic"/>, or subscribes to it when there is none.</summary>
    internal async ValueTask RunAsync(Mqtt5Connect connect, string topic, ReadOnlyMemory<byte>? payload)
    {
        Mqtt5Packet publishOrSubscribe = BuildPublishOrSubscribe(topic, payload);
        await channel.SendAsync(connect).ConfigureAwait(false);
        var connAck = (Mqtt5ConnAck)await ReceiveAsync(Mqtt5PacketType.ConnAck).ConfigureAwait(false);
        ThrowIfFailure("CONNACK", connAck.ReasonCode);
        await channel.SendAsync(publishOrSubscribe).ConfigureAwait(false);
        if (publishOrSubscribe is Mqtt5Publish)
        {
            await channel.SendAsync(new Mqtt5Disconnect { ReasonCode = Mqtt5ReasonCode.NormalDisconnection }).ConfigureAwait(false);
            return;
        }

        var subAck = (Mqtt5SubscriptionAcknowledgement)await ReceiveAsync(Mqtt5PacketType.SubAck).ConfigureAwait(false);
        foreach (Mqtt5ReasonCode reasonCode in subAck.ReasonCodes)
        {
            ThrowIfFailure("SUBACK", reasonCode);
        }

        await WritePublishedUntilDisconnectAsync().ConfigureAwait(false);
    }

    private static Mqtt5Packet BuildPublishOrSubscribe(string topic, ReadOnlyMemory<byte>? payload)
    {
        try
        {
            Mqtt5Packet packet = payload is { } data
                ? new Mqtt5Publish { TopicName = topic, Payload = data.ToArray() }
                : new Mqtt5Subscribe { PacketIdentifier = SubscribePacketIdentifier, Subscriptions = [new Mqtt5Subscription { TopicFilter = topic }] };
            Mqtt5Topics.ValidateTopicFilter(topic);
            packet.ToBytes();
            return packet;
        }
        catch (Mqtt5MalformedPacketException invalid)
        {
            throw new Mqtt5ClientException(CurlExitCode.UrlMalformat, Mqtt5ClientMessages.TopicInvalid(invalid.Message));
        }
    }

    private static void ThrowIfFailure(string packetName, Mqtt5ReasonCode reasonCode)
    {
        if (Mqtt5ReasonCodes.IsFailure(reasonCode))
        {
            throw new Mqtt5ClientException(CurlExitCode.WeirdServerReply, Mqtt5ClientMessages.Refused(packetName, reasonCode));
        }
    }

    private async ValueTask<Mqtt5Packet> ReceiveAsync(Mqtt5PacketType expected)
    {
        Mqtt5Packet packet = await channel.ReceiveAsync().ConfigureAwait(false);
        return packet.PacketType == expected
            ? packet
            : throw new Mqtt5ClientException(CurlExitCode.WeirdServerReply, Mqtt5ClientMessages.WeirdServerReply);
    }

    private async ValueTask WritePublishedUntilDisconnectAsync()
    {
        while (true)
        {
            switch (await channel.ReceiveAsync().ConfigureAwait(false))
            {
                case Mqtt5Publish publish:
                    await WriteOutputAsync(publish.Payload).ConfigureAwait(false);
                    break;
                case Mqtt5Disconnect disconnect:
                    ThrowIfFailure("DISCONNECT", disconnect.ReasonCode);
                    return;
                default:
                    throw new Mqtt5ClientException(CurlExitCode.WeirdServerReply, Mqtt5ClientMessages.WeirdServerReply);
            }
        }
    }

    private async ValueTask WriteOutputAsync(byte[] payload)
    {
        try
        {
            await output.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            throw new Mqtt5ClientException(CurlExitCode.WriteError, Mqtt5ClientMessages.OutputWriteFailed(payload.Length));
        }

        BytesWritten += payload.Length;
        progress.ReportDownloaded(BytesWritten, null);
    }
}
