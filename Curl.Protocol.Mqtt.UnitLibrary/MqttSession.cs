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
/// one is gathered whole before it is written, so one PUBLISH is one write to the output
/// however the peer split it; one cut short by the peer closing is written as far as it
/// got, as curl's is, before the transfer fails with exit 18.
/// </para>
/// <para>
/// A failure is thrown as an <see cref="MqttTransferException" /> carrying curl's exit
/// code and message; <see cref="BytesWritten" /> still says how much reached the output.
/// </para>
/// </remarks>
internal sealed class MqttSession(IConnection connection, Stream output, CancellationToken cancellationToken)
{
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
        MqttFixedHeader? connack = await ReadPacketHeaderAsync().ConfigureAwait(false);
        if (connack is null)
        {
            return;
        }

        await VerifyConnackAsync(connack.Value).ConfigureAwait(false);
        byte[] topic = MqttTopic.Decode(url);
        if (postData is { } payload)
        {
            await SendAsync(MqttPackets.BuildPublish(topic, payload)).ConfigureAwait(false);
            await SendAsync(MqttPackets.BuildDisconnect()).ConfigureAwait(false);
            return;
        }

        await SendAsync(MqttPackets.BuildSubscribe(topic)).ConfigureAwait(false);
        await ReceivePublishesAsync().ConfigureAwait(false);
    }

    private async ValueTask ReceivePublishesAsync()
    {
        while (await ReadPacketHeaderAsync().ConfigureAwait(false) is { } header)
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
        }
    }

    /// <summary>
    /// Reads fixed headers until one has a body, returning <see langword="null" /> for a
    /// DISCONNECT and passing over any other empty packet. Passing over a PINGRESP after
    /// the SUBACK is what curl 8.21.0 was measured to do; an empty packet anywhere else
    /// moves curl's state machine in ways this does not reproduce.
    /// </summary>
    private async ValueTask<MqttFixedHeader?> ReadPacketHeaderAsync()
    {
        while (true)
        {
            MqttFixedHeader header = await reader.ReadFixedHeaderAsync().ConfigureAwait(false);
            if (header.RemainingLength > 0)
            {
                return header;
            }

            if (header.PacketType == MqttPackets.DisconnectType)
            {
                return null;
            }
        }
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

    private async ValueTask WriteOutputAsync(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return;
        }

        try
        {
            await output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            throw new MqttTransferException(
                CurlExitCode.WriteError,
                MqttTransferMessages.OutputWriteFailed(bytes.Length));
        }

        BytesWritten += bytes.Length;
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
}
