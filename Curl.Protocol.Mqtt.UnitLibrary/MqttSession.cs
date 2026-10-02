using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Mqtt;

/// <summary>
/// One MQTT transfer over an open connection, in the order curl 8.21.0's
/// <c>lib/mqtt.c</c> runs it: CONNECT, wait for CONNACK, read the topic from the URL, then
/// either PUBLISH the request data and DISCONNECT, or SUBSCRIBE and write every PUBLISH to
/// the output until the peer disconnects.
/// </summary>
/// <param name="connection">The connection to talk over; the caller owns and disposes it.</param>
/// <param name="output">The stream each received PUBLISH is written to.</param>
/// <param name="progress">
/// Where every write to <paramref name="output" /> is reported, as the running total with the
/// PUBLISH body's length as the expected size, as curl's <c>Curl_pgrsSetDownloadSize</c> sets it
/// (so <c>-m</c> reports <c>with 5 out of 5 bytes received</c>, BL-511 Notes).
/// </param>
/// <param name="events">
/// Where the session is reported as curl 8.21.0's <c>-v</c> and <c>--trace</c> show it
/// (measured, BL-935): the client identifier, each packet sent as a header block, each
/// header byte and CONNACK or SUBACK body received as a header block, each PUBLISH body
/// slice as data, and each <c>mqtt_doing: state [N]</c> line curl's state machine writes.
/// </param>
/// <param name="log">Where each packet sent and received and each step is logged (ADR-0222, BL-928).</param>
/// <param name="maxFileSize">
/// The most bytes one PUBLISH body may hold (<c>--max-filesize</c>), or <see langword="null" />
/// or 0 for no limit. A PUBLISH whose remaining length is larger fails the transfer with exit 63
/// before any of its body is read, as curl 8.21.0's <c>mqtt_doing</c> does (BL-1115).
/// </param>
/// <param name="timeProvider">
/// The clock the idle time before a PINGREQ is measured on and waited out with.
/// </param>
/// <param name="cancellationToken">Cancels every read and write.</param>
/// <remarks>
/// <para>
/// While a packet's first byte is awaited, the session sends a PINGREQ (<c>C0 00</c>) and
/// reports <c>mqtt_ping: sent ping request.</c> once more than 60 seconds have passed since
/// it last sent or received anything, as curl 8.21.0's <c>mqtt_ping</c> does with the
/// default <c>CURLOPT_UPKEEP_INTERVAL_MS</c> of 60000 (BL-1116). No second PINGREQ is sent
/// until a PINGRESP arrives. The <c>mqtt_doing: state [0]</c> line curl writes for each idle
/// poll in between is not reproduced; only the one after the PINGREQ is.
/// </para>
/// <para>
/// A received PUBLISH is written as curl writes it: its whole body after the fixed header -
/// the two-byte topic length, the topic, then the payload - with nothing parsed out. Each
/// one is gathered whole, however the peer split it, then written in slices of at most
/// 4096 bytes, so one PUBLISH of up to 4096 bytes is one write to the output; that is how
/// curl's writes measured on loopback, where each of its 4096-byte reads came back full.
/// One cut short by the peer closing is written as far as it got, as curl's is, before the
/// transfer fails with exit 18.
/// </para>
/// <para>
/// A packet with no body moves the session as curl's does, however odd the result: a
/// PINGRESP makes the next packet with a body a PUBLISH or SUBACK even before the CONNACK,
/// so neither the CONNACK check nor the SUBSCRIBE happens; any other empty packet but
/// DISCONNECT drops what was awaited, so the next packet's body is read as headers, and the
/// first of those with a body ends the transfer with exit 0 and nothing written.
/// </para>
/// <para>
/// A failure is thrown as an <see cref="MqttTransferException" /> carrying curl's exit
/// code and message; <see cref="BytesWritten" /> still says how much reached the output.
/// </para>
/// </remarks>
internal sealed class MqttSession(
    IConnection connection,
    Stream output,
    ITransferProgress progress,
    ITransferEvents events,
    MqttDiagnosticLog log,
    long? maxFileSize,
    TimeProvider timeProvider,
    CancellationToken cancellationToken)
{
    /// <summary><c>MQTT_FIRST</c>: awaiting a packet's first byte.</summary>
    private const int FirstState = 0;

    /// <summary><c>MQTT_PUB_REMAIN</c>: reading the rest of a PUBLISH body.</summary>
    private const int PublishRemainState = 6;

    /// <summary>
    /// The most bytes one write to the output carries: the size of the buffer curl 8.21.0's
    /// <c>mqtt_read_publish</c> reads a PUBLISH body into, one read per write.
    /// </summary>
    private const int OutputWriteSize = 4096;

    /// <summary>
    /// How long the connection must sit idle before a PINGREQ: curl's
    /// <c>CURL_UPKEEP_INTERVAL_DEFAULT</c> of 60000 ms, which <c>mqtt_ping</c> must exceed,
    /// so the first whole millisecond past it.
    /// </summary>
    private static readonly TimeSpan PingAfterIdle = TimeSpan.FromMilliseconds(60001);

    private readonly MqttPacketReader reader = new(connection, events, log, cancellationToken);

    /// <summary>Whether a PINGREQ is sent and its PINGRESP not yet received.</summary>
    private bool pingSent;

    /// <summary>The body length of the PUBLISH being written, reported as the expected download size.</summary>
    private long publishLength;

    /// <summary>
    /// Gets the number of bytes written to the output so far.
    /// </summary>
    internal long BytesWritten { get; private set; }

    /// <summary>
    /// Runs the session to its end.
    /// </summary>
    /// <param name="url">The transfer's URL, whose path names the topic.</param>
    /// <param name="clientIdentifier">The client identifier the CONNECT carries.</param>
    /// <param name="credentials">
    /// The user name and password the CONNECT carries, or <see langword="null" /> for none.
    /// </param>
    /// <param name="postData">
    /// The payload to publish, or <see langword="null" /> to subscribe instead.
    /// </param>
    /// <returns>
    /// A task that completes when a publish has sent its DISCONNECT, or when the peer has
    /// sent DISCONNECT.
    /// </returns>
    /// <exception cref="MqttTransferException">The session failed; see its exit code.</exception>
    internal async ValueTask RunAsync(
        CurlUrl url,
        string clientIdentifier,
        NetworkCredential? credentials,
        ReadOnlyMemory<byte>? postData)
    {
        events.ReportInfo(MqttTransferMessages.UsingClientId(clientIdentifier));
        await SendAsync(MqttPackets.BuildConnect(clientIdentifier, credentials)).ConfigureAwait(false);

        // curl runs mqtt_doing once straight after the CONNECT, before any reply can have
        // arrived, and finds nothing to read (measured every time on loopback, BL-935).
        ReportDoingState(FirstState);
        SessionState state = SessionState.AwaitingConnack;
        while (state != SessionState.Done)
        {
            ReportDoingState(FirstState);
            MqttFixedHeader header = await ReadFixedHeaderPingingWhenIdleAsync().ConfigureAwait(false);
            state = header.RemainingLength == 0
                ? StateAfterEmptyPacket(header)
                : await ReadPacketAsync(state, header, url, postData).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads the next fixed header, sending a PINGREQ if the connection sits idle past
    /// <see cref="PingAfterIdle" /> while its first byte is awaited and no PINGREQ is
    /// outstanding.
    /// </summary>
    /// <remarks>
    /// The idle time is counted from when the wait begins, which is straight after the
    /// session last sent or received anything: the CONNECT, a packet handled whole, or the
    /// PINGREQ itself, the moments curl sets <c>lastTime</c>. The clock is read only when a
    /// wait begins, so a peer that has its next packet ready never costs a timer.
    /// </remarks>
    private async ValueTask<MqttFixedHeader> ReadFixedHeaderPingingWhenIdleAsync()
    {
        Task firstByte = reader.WhenFirstByteReadyAsync();
        if (!pingSent && !firstByte.IsCompleted)
        {
            await PingIfIdleBeforeAsync(firstByte).ConfigureAwait(false);
        }

        return await reader.ReadFixedHeaderAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Waits until <paramref name="firstByte" /> completes or <see cref="PingAfterIdle" />
    /// passes, and in the second case sends a PINGREQ.
    /// </summary>
    private async ValueTask PingIfIdleBeforeAsync(Task firstByte)
    {
        using CancellationTokenSource stopTimer = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task timer = Task.Delay(PingAfterIdle, timeProvider, stopTimer.Token);
        if (await Task.WhenAny(firstByte, timer).ConfigureAwait(false) == firstByte)
        {
            await stopTimer.CancelAsync().ConfigureAwait(false);
            return;
        }

        await timer.ConfigureAwait(false);
        await SendAsync(MqttPackets.BuildPingRequest()).ConfigureAwait(false);
        pingSent = true;
        events.ReportInfo(MqttTransferMessages.SentPingRequest);
        ReportDoingState(FirstState);
    }

    /// <summary>
    /// The state after a packet with no body, as curl's <c>MQTT_REMAINING_LENGTH</c> case
    /// sets it: a DISCONNECT ends the transfer, a PINGRESP awaits a PUBLISH or SUBACK even
    /// before the CONNACK has come, and any other empty packet drops whatever was awaited.
    /// </summary>
    private SessionState StateAfterEmptyPacket(MqttFixedHeader header)
    {
        switch (header.PacketType)
        {
            case MqttPackets.DisconnectType:
                events.ReportInfo(MqttTransferMessages.GotDisconnect);
                return SessionState.Done;
            case MqttPackets.PingResponseType:
                events.ReportInfo(MqttTransferMessages.ReceivedPingResponse);
                pingSent = false;
                return SessionState.AwaitingPublish;
            default:
                log.EmptyPacketIgnored(header);
                return SessionState.LeavingBodyUnread;
        }
    }

    /// <summary>
    /// Handles a packet that has a body according to the state the session is in, and
    /// returns the next state.
    /// </summary>
    private async ValueTask<SessionState> ReadPacketAsync(
        SessionState state,
        MqttFixedHeader header,
        CurlUrl url,
        ReadOnlyMemory<byte>? postData)
    {
        ReportDoingStateUnlessFirst(state);
        return state switch
        {
            SessionState.AwaitingConnack => await AcceptConnackAsync(header, url, postData).ConfigureAwait(false),
            SessionState.AwaitingSuback or SessionState.AwaitingPublish => await ReceivePublishOrSubackAsync(header).ConfigureAwait(false),
            SessionState.LeavingBodyUnread => LeaveBodyUnread(header),
            _ => EndUnhandled(header),
        };
    }

    /// <summary>
    /// Reports the <c>mqtt_doing</c> line of the run that handles a body in
    /// <paramref name="state" />; curl's <c>MQTT_FIRST</c> handles none, so its line is the
    /// one before the next fixed header.
    /// </summary>
    private void ReportDoingStateUnlessFirst(SessionState state)
    {
        int number = state switch
        {
            SessionState.AwaitingConnack => 2,
            SessionState.AwaitingSuback => 3,
            SessionState.AwaitingPublish => 5,
            SessionState.LeavingBodyUnread => FirstState,
            _ => 7,
        };
        if (number != FirstState)
        {
            ReportDoingState(number);
        }
    }

    private void ReportDoingState(int state) => events.ReportInfo(MqttTransferMessages.DoingState(state));

    /// <summary>Passes over a packet's body, as an empty packet before it made curl do.</summary>
    private SessionState LeaveBodyUnread(MqttFixedHeader header)
    {
        log.BodyLeftUnread(header);
        return SessionState.StateNotHandled;
    }

    /// <summary>Ends the transfer with exit 0, as curl's <c>State not handled yet</c> does.</summary>
    private SessionState EndUnhandled(MqttFixedHeader header)
    {
        log.StateNotHandled(header);
        events.ReportInfo(MqttTransferMessages.StateNotHandled);
        return SessionState.Done;
    }

    /// <summary>
    /// Checks the CONNACK, then either publishes and disconnects, ending the transfer, or
    /// subscribes and awaits the SUBACK.
    /// </summary>
    private async ValueTask<SessionState> AcceptConnackAsync(
        MqttFixedHeader header,
        CurlUrl url,
        ReadOnlyMemory<byte>? postData)
    {
        await VerifyConnackAsync(header).ConfigureAwait(false);
        byte[] topic = MqttTopic.Decode(url);
        if (postData is { } payload)
        {
            byte[] publish = MqttPackets.BuildPublish(topic, payload);
            log.Publishing(topic, payload.Length);
            await SendAsync(publish).ConfigureAwait(false);
            await SendAsync(MqttPackets.BuildDisconnect()).ConfigureAwait(false);
            log.PublishDone();
            return SessionState.Done;
        }

        log.Subscribing(topic);
        await SendAsync(MqttPackets.BuildSubscribe(topic)).ConfigureAwait(false);
        return SessionState.AwaitingSuback;
    }

    private async ValueTask<SessionState> ReceivePublishOrSubackAsync(MqttFixedHeader header)
    {
        switch (header.PacketType)
        {
            case MqttPackets.PublishType:
                await WritePublishAsync(header).ConfigureAwait(false);
                break;
            case MqttPackets.SubackType:
                await VerifySubackAsync(header).ConfigureAwait(false);
                break;
            default:
                throw new MqttTransferException(CurlExitCode.WeirdServerReply, MqttTransferMessages.WeirdServerReply);
        }

        return SessionState.AwaitingPublish;
    }

    /// <summary>
    /// Checks the CONNACK. Like curl, only its length and its two bytes are checked, not
    /// its packet type.
    /// </summary>
    private async ValueTask VerifyConnackAsync(MqttFixedHeader header)
    {
        if (header.RemainingLength != 2)
        {
            throw new MqttTransferException(
                CurlExitCode.WeirdServerReply,
                MqttTransferMessages.ConnackLengthUnexpected(header.RemainingLength));
        }

        WarnIfNotConnack(header);
        byte[] body = await reader.ReadBodyAsync(2, CurlExitCode.RecvError, MqttTransferMessages.ReceiveFailed)
            .ConfigureAwait(false);
        events.ReportResponseHeader(body);
        log.ConnackReceived(body[1]);
        if (body[0] != 0 || body[1] != 0)
        {
            throw new MqttTransferException(
                CurlExitCode.WeirdServerReply,
                MqttTransferMessages.ConnackRefused(body[0], body[1]));
        }
    }

    private void WarnIfNotConnack(MqttFixedHeader header)
    {
        if (header.PacketType != MqttPackets.ConnackType)
        {
            log.TakenAsConnack(header);
        }
    }

    private async ValueTask VerifySubackAsync(MqttFixedHeader header)
    {
        if (header.RemainingLength != 3)
        {
            throw new MqttTransferException(
                CurlExitCode.WeirdServerReply,
                MqttTransferMessages.SubackLengthUnexpected(header.RemainingLength));
        }

        byte[] body = await reader.ReadBodyAsync(3, CurlExitCode.RecvError, MqttTransferMessages.ReceiveFailed)
            .ConfigureAwait(false);
        events.ReportResponseHeader(body);
        if (!AcknowledgesSubscribe(body))
        {
            throw new MqttTransferException(CurlExitCode.WeirdServerReply, MqttTransferMessages.WeirdServerReply);
        }

        log.SubscribeDone();
    }

    /// <summary>
    /// Whether a SUBACK body names curl's one packet identifier and grants QoS 0.
    /// </summary>
    private static bool AcknowledgesSubscribe(byte[] body) =>
        body[0] == 0x00 && body[1] == MqttPackets.SubscribePacketIdentifier && body[2] == 0x00;

    /// <summary>
    /// Gathers a PUBLISH body as it arrives, growing only as bytes do rather than trusting
    /// the length the peer claimed, then writes it.
    /// </summary>
    private async ValueTask WritePublishAsync(MqttFixedHeader header)
    {
        publishLength = header.RemainingLength;
        events.ReportInfo(MqttTransferMessages.RemainingLength(header.RemainingLength));
        if (maxFileSize is > 0 and long limit && header.RemainingLength > limit)
        {
            throw new MqttTransferException(CurlExitCode.FilesizeExceeded, MqttTransferMessages.MaximumFileSizeExceeded);
        }

        using MemoryStream body = new();
        while (body.Length < header.RemainingLength)
        {
            ReadOnlyMemory<byte> chunk = await reader
                .ReadChunkAsync(header.RemainingLength - (int)body.Length)
                .ConfigureAwait(false);
            if (chunk.IsEmpty)
            {
                await WriteOutputAsync(body.ToArray()).ConfigureAwait(false);
                ReportServerDisconnected(body.Length);
                throw new MqttTransferException(CurlExitCode.PartialFile, MqttTransferMessages.PartialFile);
            }

            body.Write(chunk.Span);
        }

        await WriteOutputAsync(body.ToArray()).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes bytes to the output in slices of at most <see cref="OutputWriteSize" />, as
    /// curl passes a PUBLISH body on in reads of at most its 4096-byte buffer.
    /// </summary>
    private async ValueTask WriteOutputAsync(byte[] bytes)
    {
        for (int offset = 0; offset < bytes.Length; offset += OutputWriteSize)
        {
            if (offset > 0)
            {
                ReportDoingState(PublishRemainState);
            }

            await WriteOutputSliceAsync(bytes.AsMemory(offset, Math.Min(OutputWriteSize, bytes.Length - offset)))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Writes one slice, reporting a failure with how many of its bytes the output accepted:
    /// <see cref="OutputWriteFailedException.BytesAccepted" /> when the output reported it,
    /// and 0 for any other <see cref="IOException" />.
    /// </summary>
    private async ValueTask WriteOutputSliceAsync(ReadOnlyMemory<byte> slice)
    {
        events.ReportDataReceived(slice.Span);
        try
        {
            await output.WriteAsync(slice, cancellationToken).ConfigureAwait(false);
        }
        catch (OutputWriteFailedException failure)
        {
            throw new MqttTransferException(
                CurlExitCode.WriteError,
                MqttTransferMessages.OutputWriteFailed(slice.Length, failure.BytesAccepted));
        }
        catch (IOException)
        {
            throw new MqttTransferException(
                CurlExitCode.WriteError,
                MqttTransferMessages.OutputWriteFailed(slice.Length, 0));
        }

        BytesWritten += slice.Length;
        progress.ReportDownloaded(BytesWritten, publishLength);
    }

    /// <summary>
    /// Reports the peer closing inside a PUBLISH body as curl does: after the run that read
    /// part of it, one more run (<c>MQTT_PUB_REMAIN</c>) finds the close.
    /// </summary>
    private void ReportServerDisconnected(long bytesRead)
    {
        if (bytesRead > 0)
        {
            ReportDoingState(PublishRemainState);
        }

        events.ReportInfo(MqttTransferMessages.ServerDisconnected);
    }

    private async ValueTask SendAsync(byte[] packet)
    {
        log.PacketSent(packet);
        events.ReportRequestHeader(packet);
        try
        {
            await connection.WriteAsync(packet, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            throw new MqttTransferException(CurlExitCode.SendError, MqttTransferMessages.SendFailed);
        }
    }

    /// <summary>
    /// Where the session stands between packets, after curl 8.21.0's <c>mqttstate</c> and
    /// the <c>nextstate</c> it keeps while reading a fixed header.
    /// </summary>
    private enum SessionState
    {
        /// <summary>The CONNECT is sent; the next packet with a body is taken as the CONNACK.</summary>
        AwaitingConnack,

        /// <summary>
        /// The SUBSCRIBE is sent (curl's <c>MQTT_SUBACK</c>): the next packet with a body
        /// must be a PUBLISH or a SUBACK.
        /// </summary>
        AwaitingSuback,

        /// <summary>
        /// Subscribed (curl's <c>MQTT_PUBWAIT</c>): the next packet with a body must be a
        /// PUBLISH or a SUBACK, as in <see cref="AwaitingSuback" />.
        /// </summary>
        AwaitingPublish,

        /// <summary>
        /// An empty packet dropped what was awaited (curl's <c>MQTT_FIRST</c> as next
        /// state): the next packet's body is left unread, so its bytes are read as the
        /// fixed header after it.
        /// </summary>
        LeavingBodyUnread,

        /// <summary>
        /// A body was left unread (curl's <c>MQTT_NOSTATE</c> as next state): the next
        /// packet with a body ends the transfer with exit 0, as curl's
        /// <c>State not handled yet</c> does.
        /// </summary>
        StateNotHandled,

        /// <summary>The transfer has ended successfully.</summary>
        Done,
    }
}
