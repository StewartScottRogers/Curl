using Curl.Extension.Mqtt5.Packets;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Mqtt5.Client;

/// <summary>
/// curl's <c>mqtt://</c> flow in MQTT 5 over any byte transport: CONNECT and CONNACK, then with a body PUBLISH and
/// DISCONNECT, otherwise SUBSCRIBE, SUBACK and each PUBLISH payload written to the output until the server sends
/// DISCONNECT. A failure reason code ends the transfer as ADR-1049 maps it. While it waits for messages it sends a
/// PINGREQ every half of the Server Keep Alive a CONNACK returns, on <paramref name="timeProvider"/>.
/// </summary>
internal sealed class Mqtt5ClientFlow(IConnection transport, Stream output, ITransferProgress progress, TimeProvider timeProvider, CancellationToken cancellationToken)
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

        await WritePublishedUntilDisconnectAsync(PingIntervalOf(connAck)).ConfigureAwait(false);
    }

    /// <summary>Half the Server Keep Alive <paramref name="connAck"/> returns, or none when it returns none or 0.</summary>
    internal static TimeSpan? PingIntervalOf(Mqtt5ConnAck connAck) =>
        connAck.Properties.FirstOrDefault(property => property.Identifier == Mqtt5PropertyIdentifier.ServerKeepAlive) is { IntegerValue: > 0 } serverKeepAlive
            ? TimeSpan.FromSeconds(serverKeepAlive.IntegerValue) / 2
            : null;

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

    /// <summary>
    /// Writes each PUBLISH until a DISCONNECT, sending a PINGREQ each time <paramref name="pingInterval"/> passes since the
    /// last one whatever the server sends meanwhile, since Keep Alive counts only what the client sends. The pending
    /// ping delay is cancelled once the DISCONNECT arrives.
    /// </summary>
    private async ValueTask WritePublishedUntilDisconnectAsync(TimeSpan? pingInterval)
    {
        using CancellationTokenSource stopPings = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task<Mqtt5Packet> receive = channel.ReceiveAsync().AsTask();
        Task ping = DelayUntilNextPing(pingInterval, stopPings.Token);
        while (true)
        {
            if (await Task.WhenAny(receive, ping).ConfigureAwait(false) == ping)
            {
                await ping.ConfigureAwait(false);
                await channel.SendAsync(new Mqtt5Ping(Mqtt5PacketType.PingReq)).ConfigureAwait(false);
                ping = DelayUntilNextPing(pingInterval, stopPings.Token);
            }
            else if (await WriteSubscribedPacketAsync(await receive.ConfigureAwait(false)).ConfigureAwait(false))
            {
                await stopPings.CancelAsync().ConfigureAwait(false);
                return;
            }
            else
            {
                receive = channel.ReceiveAsync().AsTask();
            }
        }
    }

    private Task DelayUntilNextPing(TimeSpan? pingInterval, CancellationToken stopPings) =>
        Task.Delay(pingInterval ?? Timeout.InfiniteTimeSpan, timeProvider, stopPings);

    /// <summary>Writes a PUBLISH's payload and accepts a PINGRESP; returns whether the packet was the closing DISCONNECT.</summary>
    private async ValueTask<bool> WriteSubscribedPacketAsync(Mqtt5Packet packet)
    {
        switch (packet)
        {
            case Mqtt5Publish publish:
                await WriteOutputAsync(publish.Payload).ConfigureAwait(false);
                return false;
            case Mqtt5Ping { PacketType: Mqtt5PacketType.PingResp }:
                return false;
            case Mqtt5Disconnect disconnect:
                ThrowIfFailure("DISCONNECT", disconnect.ReasonCode);
                return true;
            default:
                throw new Mqtt5ClientException(CurlExitCode.WeirdServerReply, Mqtt5ClientMessages.WeirdServerReply);
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
