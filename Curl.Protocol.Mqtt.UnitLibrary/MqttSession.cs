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
/// <param name="cancellationToken">Cancels every read and write.</param>
/// <remarks>
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
internal sealed class MqttSession(IConnection connection, Stream output, CancellationToken cancellationToken)
{
    /// <summary>
    /// The most bytes one write to the output carries: the size of the buffer curl 8.21.0's
    /// <c>mqtt_read_publish</c> reads a PUBLISH body into, one read per write.
    /// </summary>
    private const int OutputWriteSize = 4096;

    private readonly MqttPacketReader reader = new(connection, cancellationToken);

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
        Uri url,
        string clientIdentifier,
        NetworkCredential? credentials,
        ReadOnlyMemory<byte>? postData)
    {
        await SendAsync(MqttPackets.BuildConnect(clientIdentifier, credentials)).ConfigureAwait(false);
        SessionState state = SessionState.AwaitingConnack;
        while (state != SessionState.Done)
        {
            MqttFixedHeader header = await reader.ReadFixedHeaderAsync().ConfigureAwait(false);
            state = header.RemainingLength == 0
                ? StateAfterEmptyPacket(header)
                : await ReadPacketAsync(state, header, url, postData).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The state after a packet with no body, as curl's <c>MQTT_REMAINING_LENGTH</c> case
    /// sets it: a DISCONNECT ends the transfer, a PINGRESP awaits a PUBLISH or SUBACK even
    /// before the CONNACK has come, and any other empty packet drops whatever was awaited.
    /// </summary>
    private static SessionState StateAfterEmptyPacket(MqttFixedHeader header) => header.PacketType switch
    {
        MqttPackets.DisconnectType => SessionState.Done,
        MqttPackets.PingResponseType => SessionState.AwaitingPublishOrSuback,
        _ => SessionState.LeavingBodyUnread,
    };

    /// <summary>
    /// Handles a packet that has a body according to the state the session is in, and
    /// returns the next state.
    /// </summary>
    private async ValueTask<SessionState> ReadPacketAsync(
        SessionState state,
        MqttFixedHeader header,
        Uri url,
        ReadOnlyMemory<byte>? postData) => state switch
        {
            SessionState.AwaitingConnack => await AcceptConnackAsync(header, url, postData).ConfigureAwait(false),
            SessionState.AwaitingPublishOrSuback => await ReceivePublishOrSubackAsync(header).ConfigureAwait(false),
            SessionState.LeavingBodyUnread => SessionState.StateNotHandled,
            _ => SessionState.Done,
        };

    /// <summary>
    /// Checks the CONNACK, then either publishes and disconnects, ending the transfer, or
    /// subscribes and awaits the SUBACK.
    /// </summary>
    private async ValueTask<SessionState> AcceptConnackAsync(
        MqttFixedHeader header,
        Uri url,
        ReadOnlyMemory<byte>? postData)
    {
        await VerifyConnackAsync(header).ConfigureAwait(false);
        byte[] topic = MqttTopic.Decode(url);
        if (postData is { } payload)
        {
            await SendAsync(MqttPackets.BuildPublish(topic, payload)).ConfigureAwait(false);
            await SendAsync(MqttPackets.BuildDisconnect()).ConfigureAwait(false);
            return SessionState.Done;
        }

        await SendAsync(MqttPackets.BuildSubscribe(topic)).ConfigureAwait(false);
        return SessionState.AwaitingPublishOrSuback;
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

        return SessionState.AwaitingPublishOrSuback;
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

        byte[] body = await reader.ReadBodyAsync(2, CurlExitCode.RecvError, MqttTransferMessages.ReceiveFailed)
            .ConfigureAwait(false);
        if (body[0] != 0 || body[1] != 0)
        {
            throw new MqttTransferException(
                CurlExitCode.WeirdServerReply,
                MqttTransferMessages.ConnackRefused(body[0], body[1]));
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
        if (!AcknowledgesSubscribe(body))
        {
            throw new MqttTransferException(CurlExitCode.WeirdServerReply, MqttTransferMessages.WeirdServerReply);
        }
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
        using MemoryStream body = new();
        while (body.Length < header.RemainingLength)
        {
            ReadOnlyMemory<byte> chunk = await reader
                .ReadChunkAsync(header.RemainingLength - (int)body.Length)
                .ConfigureAwait(false);
            if (chunk.IsEmpty)
            {
                await WriteOutputAsync(body.ToArray()).ConfigureAwait(false);
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
    }

    private async ValueTask SendAsync(byte[] packet)
    {
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

        /// <summary>The next packet with a body must be a PUBLISH or a SUBACK.</summary>
        AwaitingPublishOrSuback,

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
