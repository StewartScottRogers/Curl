using System.Runtime.ExceptionServices;
using Curl.Extended.Protocol.Mqtt5.Packets;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Mqtt5.Client;

/// <summary>
/// curl's <c>mqtt://</c> flow in MQTT 5 over any byte transport: CONNECT and CONNACK, then with a body PUBLISH and
/// DISCONNECT, otherwise SUBSCRIBE, SUBACK and each PUBLISH payload written to the output, a QoS 1 PUBLISH answered with
/// PUBACK and a QoS 2 one with PUBREC and its PUBREL with PUBCOMP (ADR-1199), until the server sends
/// DISCONNECT. A failure reason code ends the transfer as ADR-1049 maps it. While it waits for messages it sends a
/// PINGREQ every half of the Server Keep Alive a CONNACK returns, on <paramref name="timeProvider"/>, and closes with
/// DISCONNECT 0x8D Keep Alive timeout when the next one falls due with the last unanswered (ADR-1199).
/// </summary>
internal sealed class Mqtt5ClientFlow(IConnection transport, Stream output, ITransferProgress progress, TimeProvider timeProvider, CancellationToken cancellationToken)
{
    /// <summary>The packet identifier of the one SUBSCRIBE the flow sends.</summary>
    internal const ushort SubscribePacketIdentifier = 1;

    private readonly Mqtt5PacketChannel channel = new(transport, cancellationToken);

    /// <summary>Whether the last PINGREQ sent has had no PINGRESP yet.</summary>
    private bool pingUnanswered;

    /// <summary>The payload bytes written to the output so far.</summary>
    internal long BytesWritten { get; private set; }

    /// <summary>Runs the flow: publishes <paramref name="payload"/> to <paramref name="topic"/>, or subscribes to it when there is none.</summary>
    internal async ValueTask RunAsync(Mqtt5Connect connect, string topic, ReadOnlyMemory<byte>? payload)
    {
        Mqtt5ClientException fault;
        Mqtt5ReasonCode reasonCode;
        try
        {
            await ExchangeAsync(connect, topic, payload).ConfigureAwait(false);
            return;
        }
        catch (Mqtt5ClientException failure) when (failure.DisconnectReasonCode is { } code)
        {
            fault = failure;
            reasonCode = code;
        }

        await SendClosingDisconnectAsync(reasonCode).ConfigureAwait(false);
        ExceptionDispatchInfo.Throw(fault);
    }

    /// <summary>
    /// Sends the DISCONNECT that answers a Malformed Packet or a Protocol Error before the connection closes (section
    /// 4.13.1, ADR-1201). It is best effort: a failed send leaves the transfer ending with the original failure.
    /// </summary>
    private async ValueTask SendClosingDisconnectAsync(Mqtt5ReasonCode reasonCode)
    {
        try
        {
            await channel.SendAsync(new Mqtt5Disconnect { ReasonCode = reasonCode }).ConfigureAwait(false);
        }
        catch (Mqtt5ClientException)
        {
            // The original failure is the one the transfer reports.
        }
    }

    private async ValueTask ExchangeAsync(Mqtt5Connect connect, string topic, ReadOnlyMemory<byte>? payload)
    {
        Mqtt5Packet publishOrSubscribe = BuildPublishOrSubscribe(topic, payload);
        await channel.SendAsync(connect).ConfigureAwait(false);
        var connAck = (Mqtt5ConnAck)await ReceiveAsync(Mqtt5PacketType.ConnAck).ConfigureAwait(false);
        ThrowIfFailure("CONNACK", connAck.ReasonCode);
        ThrowIfSessionPresent(connAck);
        ThrowIfOverMaximumPacketSize(publishOrSubscribe, connAck);
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

    /// <summary>
    /// Ends the transfer before <paramref name="packet"/> is sent when it is larger than the Maximum Packet Size
    /// <paramref name="connAck"/> returns, since the client sends no packet over it (MQTT-3.2.2-15, ADR-1193).
    /// </summary>
    private static void ThrowIfOverMaximumPacketSize(Mqtt5Packet packet, Mqtt5ConnAck connAck)
    {
        int size = packet.ToBytes().Length;
        if (connAck.Properties.FirstOrDefault(property => property.Identifier == Mqtt5PropertyIdentifier.MaximumPacketSize) is { } maximum && size > maximum.IntegerValue)
        {
            string packetName = packet is Mqtt5Publish ? "PUBLISH" : "SUBSCRIBE";
            throw new Mqtt5ClientException(CurlExitCode.SendError, Mqtt5ClientMessages.OverMaximumPacketSize(packetName, size, maximum.IntegerValue));
        }
    }

    /// <summary>Ends the transfer when <paramref name="connAck"/> resumes a session: curl sends Clean Start and keeps no Session State (MQTT-3.2.2-4, ADR-1193).</summary>
    private static void ThrowIfSessionPresent(Mqtt5ConnAck connAck)
    {
        if (connAck.SessionPresent)
        {
            throw new Mqtt5ClientException(CurlExitCode.WeirdServerReply, Mqtt5ClientMessages.SessionPresentOnCleanStart);
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
            : throw new Mqtt5ClientException(CurlExitCode.WeirdServerReply, Mqtt5ClientMessages.WeirdServerReply, Mqtt5ReasonCode.ProtocolError);
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
                ThrowIfPingUnanswered();
                pingUnanswered = true;
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

    /// <summary>
    /// Refuses to go on when the next PINGREQ falls due with the last one still unanswered: one ping interval is the
    /// reasonable time a PINGRESP gets (MQTT5-3.1.2.10-1), so the connection closes with DISCONNECT 0x8D Keep Alive
    /// timeout and exit 8 (ADR-1199).
    /// </summary>
    private void ThrowIfPingUnanswered()
    {
        if (pingUnanswered)
        {
            throw new Mqtt5ClientException(CurlExitCode.WeirdServerReply, Mqtt5ClientMessages.WeirdServerReply, Mqtt5ReasonCode.KeepAliveTimeout);
        }
    }

    private ValueTask AcceptPingResponseAsync()
    {
        pingUnanswered = false;
        return ValueTask.CompletedTask;
    }

    private Task DelayUntilNextPing(TimeSpan? pingInterval, CancellationToken stopPings) =>
        Task.Delay(pingInterval ?? Timeout.InfiniteTimeSpan, timeProvider, stopPings);

    /// <summary>
    /// Writes a PUBLISH's payload and acknowledges it by its QoS, answers a PUBREL with PUBCOMP and accepts a PINGRESP;
    /// returns whether the packet was the closing DISCONNECT (ADR-1199).
    /// </summary>
    private async ValueTask<bool> WriteSubscribedPacketAsync(Mqtt5Packet packet)
    {
        switch (packet)
        {
            case Mqtt5Publish publish:
                ThrowIfTopicAlias(publish);
                await WriteOutputAsync(publish.Payload).ConfigureAwait(false);
                await AcknowledgeAsync(publish).ConfigureAwait(false);
                return false;
            case Mqtt5Disconnect disconnect:
                ThrowIfFailure("DISCONNECT", disconnect.ReasonCode);
                return true;
            default:
                await AnswerReleaseOrAcceptPingResponseAsync(packet).ConfigureAwait(false);
                return false;
        }
    }

    /// <summary>
    /// Refuses a PUBLISH carrying a Topic Alias: curl's CONNECT sends no Topic Alias Maximum, so the server may send none
    /// (MQTT5-3.3.2.3.4-1). It is answered by DISCONNECT 0x94 Topic Alias invalid and exit 8, nothing of it written (ADR-1199).
    /// </summary>
    private static void ThrowIfTopicAlias(Mqtt5Publish publish)
    {
        if (publish.Properties.Any(property => property.Identifier == Mqtt5PropertyIdentifier.TopicAlias))
        {
            throw new Mqtt5ClientException(
                CurlExitCode.WeirdServerReply,
                Mqtt5ClientMessages.MalformedPacket("PUBLISH has a Topic Alias above the Topic Alias Maximum 0."),
                Mqtt5ReasonCode.TopicAliasInvalid);
        }
    }

    /// <summary>Answers a PUBREL with PUBCOMP (MQTT-4.3.3-11) and accepts a PINGRESP; any other packet is a Protocol Error.</summary>
    private ValueTask AnswerReleaseOrAcceptPingResponseAsync(Mqtt5Packet packet) => packet switch
    {
        Mqtt5PublishResponse { PacketType: Mqtt5PacketType.PubRel } release => SendPublishResponseAsync(Mqtt5PacketType.PubComp, release.PacketIdentifier),
        Mqtt5Ping { PacketType: Mqtt5PacketType.PingResp } => AcceptPingResponseAsync(),
        _ => throw new Mqtt5ClientException(CurlExitCode.WeirdServerReply, Mqtt5ClientMessages.WeirdServerReply, Mqtt5ReasonCode.ProtocolError),
    };

    /// <summary>
    /// Answers a QoS 1 PUBLISH with PUBACK and a QoS 2 PUBLISH with PUBREC, in the short form: Reason Code Success and no
    /// properties (MQTT-4.3.2-4, MQTT-4.3.3-8, MQTT-4.6.0-2). A QoS 0 PUBLISH is not acknowledged.
    /// </summary>
    private ValueTask AcknowledgeAsync(Mqtt5Publish publish) => publish.QualityOfService switch
    {
        Mqtt5QualityOfService.AtLeastOnce => SendPublishResponseAsync(Mqtt5PacketType.PubAck, publish.PacketIdentifier),
        Mqtt5QualityOfService.ExactlyOnce => SendPublishResponseAsync(Mqtt5PacketType.PubRec, publish.PacketIdentifier),
        _ => ValueTask.CompletedTask,
    };

    private ValueTask SendPublishResponseAsync(Mqtt5PacketType packetType, ushort packetIdentifier) =>
        channel.SendAsync(new Mqtt5PublishResponse(packetType) { PacketIdentifier = packetIdentifier, ReasonCode = Mqtt5ReasonCode.Success });

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
