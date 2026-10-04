using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws;

/// <summary>
/// Reads server frames from an upgraded connection until the server closes it, handing every
/// payload curl writes to the caller and answering pings with a masked pong (ADR-0131).
/// </summary>
/// <param name="connection">The upgraded connection.</param>
/// <param name="randomSource">Supplies the 4-byte mask of each pong.</param>
/// <param name="progress">
/// Receives <see cref="BytesReceived" /> after every read, so the progress meter and a <c>-m</c>
/// timeout message count frame bytes as curl does.
/// </param>
/// <param name="events">
/// Receives every read as data received, the empty read that ends the transfer included, as
/// curl 8.21.0 reports them for <c>-v</c> and <c>--trace</c> (BL-584).
/// </param>
/// <param name="diagnosticLog">
/// Receives each frame received under the <c>ws</c> component (<see cref="WsTransferLog" />),
/// or nothing when <see langword="null" />.
/// </param>
/// <param name="trace">
/// Receives curl's <c>--trace-config ws</c> lines for each frame decoded and each pong sent
/// (<see cref="WsFrameTrace" />, BL-1164); <see langword="null" /> writes none.
/// </param>
/// <param name="maxFileSize">
/// <c>--max-filesize</c>: how many frame bytes, heads included, may be decoded before the
/// transfer fails with exit 63; <see langword="null" /> or 0 is no limit (BL-1294).
/// </param>
/// <remarks>
/// Measured against curl 8.21.0 (BL-581): a close frame is neither answered nor the end, so
/// reading goes on until the connection closes; every ping answered is echoed in one pong,
/// but when several pings complete in the bytes of one read only the last is answered, as
/// curl replaces a pong it has not yet sent. A protocol violation fails with 56 after the
/// payload decoded before it has been handed on, and no pong is sent for that read.
/// <c>--max-filesize</c> counts the raw frame bytes, as curl 8.21.0's download writer runs
/// before its WebSocket decoder: a read that would pass the limit is cut to the bytes under it,
/// those are decoded and their payload handed on, and the transfer fails with exit 63 and no
/// pong; a read that reaches the limit exactly does not fail (BL-1294).
/// </remarks>
internal sealed class WsFrameReceiver(IConnection connection, IWebSocketRandomSource randomSource, ITransferProgress progress, ITransferEvents events, IDiagnosticLog? diagnosticLog = null, WsFrameTrace? trace = null, long? maxFileSize = null)
{
    private const int ReadBufferSize = 16384;

    private readonly WsFrameTrace trace = trace ?? WsFrameTrace.Off;

    private readonly WsFrameDecoder decoder = new(diagnosticLog, trace);

    /// <summary>
    /// Gets how many frame bytes have been received so far, frame heads included: curl's
    /// <c>%{size_download}</c> for a WebSocket transfer.
    /// </summary>
    internal long BytesReceived { get; private set; }

    /// <summary>
    /// Gets how many payload bytes have been handed on to be written so far, close frame
    /// payloads included: curl's <c>%{size_delivered}</c> for a WebSocket transfer (BL-777).
    /// </summary>
    internal long BytesDelivered { get; private set; }

    /// <summary>Gets how many bytes of pong frames have been sent so far.</summary>
    internal long BytesSent { get; private set; }

    /// <summary>
    /// Decodes the bytes that arrived with the upgrade reply's head, reporting them as one read
    /// when there are any; curl 8.21.0 does this before it sends a <c>-T</c> frame (BL-813).
    /// </summary>
    /// <param name="alreadyReceived">The bytes that arrived with the upgrade reply's head.</param>
    /// <param name="writePayload">Receives each run of payload bytes curl writes to the output.</param>
    /// <param name="cancellationToken">Cancels the writes and any pong.</param>
    /// <returns>A task that completes once the bytes are decoded.</returns>
    /// <exception cref="WsTransferException">
    /// A frame broke the protocol (56), the bytes passed <c>--max-filesize</c> (63) or a pong
    /// could not be sent (55).
    /// </exception>
    internal ValueTask DeliverAlreadyReceivedAsync(
        ReadOnlyMemory<byte> alreadyReceived,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> writePayload,
        CancellationToken cancellationToken) =>
        DeliverReceivedAsync(alreadyReceived, writePayload, cancellationToken);

    /// <summary>Reads frames until the server closes the connection.</summary>
    /// <param name="writePayload">Receives each run of payload bytes curl writes to the output.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>A task that completes once the server has closed the connection.</returns>
    /// <exception cref="WsTransferException">
    /// A frame broke the protocol (56), a read failed (56), the bytes passed <c>--max-filesize</c>
    /// (63) or a pong could not be sent (55).
    /// </exception>
    internal async ValueTask ReceiveUntilClosedAsync(
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> writePayload,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[ReadBufferSize];
        while (true)
        {
            int read = await ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                events.ReportDataReceived([]);
                return;
            }

            await DeliverReceivedAsync(buffer.AsMemory(0, read), writePayload, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask DeliverReceivedAsync(
        ReadOnlyMemory<byte> received,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> writePayload,
        CancellationToken cancellationToken)
    {
        if (!received.IsEmpty)
        {
            events.ReportDataReceived(received.Span);
        }

        int allowed = LengthUnderMaxFileSize(received.Length);
        BytesReceived += allowed;
        progress.ReportDownloaded(BytesReceived, null);
        await DeliverAsync(decoder.Decode(received.Span[..allowed]), writePayload, isCut: allowed < received.Length, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gives how many of the <paramref name="length" /> bytes just received stay under
    /// <c>--max-filesize</c>, as curl 8.21.0's <c>cw_download_write</c> cuts a write.
    /// </summary>
    private int LengthUnderMaxFileSize(int length) =>
        maxFileSize is { } limit && limit > 0
            ? (int)Math.Min(length, limit - BytesReceived)
            : length;

    private async ValueTask DeliverAsync(
        WsDecodedBytes decoded,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> writePayload,
        bool isCut,
        CancellationToken cancellationToken)
    {
        if (decoded.Payload.Length > 0)
        {
            await writePayload(decoded.Payload, cancellationToken).ConfigureAwait(false);
            BytesDelivered += decoded.Payload.Length;
        }

        if (decoded.Failure is { } failure)
        {
            throw failure;
        }

        if (isCut)
        {
            throw new WsTransferException(
                CurlExitCode.FilesizeExceeded,
                string.Create(CultureInfo.InvariantCulture, $"Exceeded the maximum allowed file size ({maxFileSize}) with {BytesReceived} bytes"));
        }

        if (decoded.LastPing is { } ping)
        {
            byte[] pong = WsFrameEncoder.Encode(WsOpcode.Pong, ping, randomSource);
            trace.FrameEncoded(WsOpcode.Pong, ping.Length);
            await WsProtocolHandler.SendAsync(connection, pong, cancellationToken).ConfigureAwait(false);
            trace.Flushed(pong.Length);
            BytesSent += pong.Length;
        }
    }

    private async ValueTask<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        try
        {
            return await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            throw WsIoFailures.ReceiveFailed(exception);
        }
    }
}
