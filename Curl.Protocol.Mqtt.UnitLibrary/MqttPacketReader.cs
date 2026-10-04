using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Mqtt;

/// <summary>
/// Reads MQTT 3.1.1 packets from an <see cref="IConnection" />, however the peer's bytes
/// happen to be split across reads: several packets in one read, or one packet across
/// several.
/// </summary>
/// <param name="connection">The connection to read from.</param>
/// <param name="events">
/// Where each fixed header byte is reported as a one-byte header block received, as curl
/// 8.21.0's <c>--trace</c> shows it reading them one at a time (measured, BL-935).
/// </param>
/// <param name="log">Where each fixed header read is logged (ADR-0222, BL-928).</param>
/// <param name="cancellationToken">Cancels every read.</param>
internal sealed class MqttPacketReader(IConnection connection, ITransferEvents events, MqttDiagnosticLog log, CancellationToken cancellationToken)
{
    private const int MaximumLengthBytes = 4;

    private readonly byte[] buffer = new byte[4096];

    private int start;

    private int end;

    /// <summary>
    /// The read <see cref="IsNextByteReady" /> or <see cref="WhenNextByteReadyAsync" /> started and no fill has taken yet,
    /// or <see langword="null" />.
    /// </summary>
    private Task<int>? pendingRead;

    /// <summary>
    /// Reads the next packet's fixed header, decoding its remaining length as MQTT 3.1.1
    /// section 2.2.3 specifies.
    /// </summary>
    /// <returns>The header.</returns>
    /// <exception cref="MqttTransferException">
    /// The peer closed where the first byte was due (exit 56, <c>Connection
    /// disconnected</c>); the peer closed inside the length, or the length ran past four
    /// bytes (exit 8); or a DISCONNECT or PINGRESP carried a body or flag bits (exit 8).
    /// </exception>
    internal async ValueTask<MqttFixedHeader> ReadFixedHeaderAsync()
    {
        int firstByte = await ReadByteAsync().ConfigureAwait(false);
        if (firstByte < 0)
        {
            throw new MqttTransferException(CurlExitCode.RecvError, MqttTransferMessages.ConnectionDisconnected);
        }

        MqttFixedHeader header = new((byte)firstByte, await ReadRemainingLengthAsync().ConfigureAwait(false));
        log.PacketReceived(header);
        RejectMalformedControlPacket(header);

        return header;
    }

    /// <summary>
    /// Reads exactly <paramref name="length" /> bytes of a packet's body.
    /// </summary>
    /// <param name="length">The number of bytes to read.</param>
    /// <param name="closedExitCode">The exit code to report if the peer closes first.</param>
    /// <param name="closedMessage">The message to report if the peer closes first.</param>
    /// <returns>The bytes.</returns>
    /// <exception cref="MqttTransferException">The peer closed before the body was whole.</exception>
    internal async ValueTask<byte[]> ReadBodyAsync(int length, CurlExitCode closedExitCode, string closedMessage)
    {
        byte[] body = new byte[length];
        int filled = 0;
        while (filled < length)
        {
            ReadOnlyMemory<byte> chunk = await ReadChunkAsync(length - filled).ConfigureAwait(false);
            if (chunk.IsEmpty)
            {
                throw new MqttTransferException(closedExitCode, closedMessage);
            }

            chunk.CopyTo(body.AsMemory(filled));
            filled += chunk.Length;
        }

        return body;
    }

    /// <summary>
    /// Reads whatever the peer has sent, up to <paramref name="maximum" /> bytes.
    /// </summary>
    /// <param name="maximum">The most bytes to return; more than zero.</param>
    /// <returns>
    /// The bytes, valid only until the next read; empty once the peer has closed.
    /// </returns>
    internal async ValueTask<ReadOnlyMemory<byte>> ReadChunkAsync(int maximum)
    {
        if (start == end && !await FillAsync().ConfigureAwait(false))
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        int count = Math.Min(maximum, end - start);
        start += count;
        return buffer.AsMemory(start - count, count);
    }

    private static void RejectMalformedControlPacket(MqttFixedHeader header)
    {
        if (header.IsMalformedControlPacket)
        {
            string name = header.PacketType == MqttPackets.DisconnectType ? "DISCONNECT" : "PINGRESP";
            throw new MqttTransferException(
                CurlExitCode.WeirdServerReply,
                MqttTransferMessages.MalformedControlPacket(name, header));
        }
    }

    private async ValueTask<int> ReadRemainingLengthAsync()
    {
        int length = 0;
        for (int index = 0; index < MaximumLengthBytes; index++)
        {
            int digit = await ReadByteAsync().ConfigureAwait(false);
            if (digit < 0)
            {
                break;
            }

            length |= (digit & 0x7F) << (7 * index);
            if ((digit & 0x80) == 0)
            {
                return length;
            }
        }

        // Four bytes all flagged "more follows", or a close before the last: curl's
        // mqtt_decode_len reports both as a truncated size.
        throw new MqttTransferException(CurlExitCode.WeirdServerReply, MqttTransferMessages.WeirdServerReply);
    }

    /// <summary>
    /// Reads one fixed header byte, reporting it as a header block received, or returns -1
    /// once the peer has closed.
    /// </summary>
    private async ValueTask<int> ReadByteAsync()
    {
        if (start == end && !await FillAsync().ConfigureAwait(false))
        {
            return -1;
        }

        events.ReportResponseHeader(buffer.AsSpan(start, 1));
        return buffer[start++];
    }

    /// <summary>
    /// Whether the next byte, or the peer's close or failure, can be read without waiting,
    /// starting a read from the connection if nothing is buffered. The read started here is
    /// the one the next read takes, so asking neither loses nor repeats it.
    /// </summary>
    /// <returns>
    /// <see langword="true" /> when a byte is buffered or the connection's read completed at
    /// once; <see langword="false" /> when that read is still pending, which is taken as
    /// curl's <c>CURLE_AGAIN</c>: no data was waiting on the socket.
    /// </returns>
    internal bool IsNextByteReady() => start < end || StartPendingRead().IsCompleted;

    /// <summary>
    /// Waits until the next byte, or the peer's close or failure, can be read without
    /// waiting, starting a read from the connection if nothing is buffered. The read started
    /// here is the one the next read takes, so a caller that stops waiting neither loses nor
    /// repeats it.
    /// </summary>
    /// <returns>
    /// A task that completes, never faulted, when the next byte is ready; a failed read
    /// throws from the next read instead.
    /// </returns>
    internal Task WhenNextByteReadyAsync()
    {
        if (start < end)
        {
            return Task.CompletedTask;
        }

        return StartPendingRead().ContinueWith(
            static _ => { },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private Task<int> StartPendingRead() => pendingRead ??= ReadConnectionAsync();

    private async ValueTask<bool> FillAsync()
    {
        Task<int> read = pendingRead ?? ReadConnectionAsync();
        pendingRead = null;
        end = await read.ConfigureAwait(false);
        start = 0;
        return end > 0;
    }

    private async Task<int> ReadConnectionAsync()
    {
        try
        {
            return await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException failure)
        {
            throw new MqttTransferException(
                CurlExitCode.RecvError,
                CurlSocketErrorText.ReceiveFailure(failure) ?? MqttTransferMessages.ReceiveFailed);
        }
    }
}
