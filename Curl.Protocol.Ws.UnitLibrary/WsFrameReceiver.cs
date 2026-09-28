using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws;

/// <summary>
/// Reads server frames from an upgraded connection until the server closes it, handing every
/// payload curl writes to the caller and answering pings with a masked pong (ADR-0131).
/// </summary>
/// <param name="connection">The upgraded connection.</param>
/// <param name="randomSource">Supplies the 4-byte mask of each pong.</param>
/// <remarks>
/// Measured against curl 8.21.0 (BL-581): a close frame is neither answered nor the end, so
/// reading goes on until the connection closes; every ping answered is echoed in one pong,
/// but when several pings complete in the bytes of one read only the last is answered, as
/// curl replaces a pong it has not yet sent. A protocol violation fails with 56 after the
/// payload decoded before it has been handed on, and no pong is sent for that read.
/// </remarks>
internal sealed class WsFrameReceiver(IConnection connection, IWebSocketRandomSource randomSource)
{
    private const int ReadBufferSize = 16384;

    private readonly WsFrameDecoder decoder = new();

    /// <summary>Reads frames until the server closes the connection.</summary>
    /// <param name="alreadyReceived">The bytes that arrived with the upgrade reply's head.</param>
    /// <param name="writePayload">Receives each run of payload bytes curl writes to the output.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>How many frame bytes were received, frame heads included.</returns>
    /// <exception cref="WsTransferException">
    /// A frame broke the protocol (56), a read failed (56) or a pong could not be sent (55).
    /// </exception>
    internal async ValueTask<long> ReceiveAsync(
        ReadOnlyMemory<byte> alreadyReceived,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> writePayload,
        CancellationToken cancellationToken)
    {
        await DeliverAsync(decoder.Decode(alreadyReceived.Span), writePayload, cancellationToken).ConfigureAwait(false);
        long received = alreadyReceived.Length;
        byte[] buffer = new byte[ReadBufferSize];
        while (true)
        {
            int read = await ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return received;
            }

            received += read;
            await DeliverAsync(decoder.Decode(buffer.AsSpan(0, read)), writePayload, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask DeliverAsync(
        WsDecodedBytes decoded,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> writePayload,
        CancellationToken cancellationToken)
    {
        if (decoded.Payload.Length > 0)
        {
            await writePayload(decoded.Payload, cancellationToken).ConfigureAwait(false);
        }

        if (decoded.Failure is { } failure)
        {
            throw failure;
        }

        if (decoded.LastPing is { } ping)
        {
            byte[] pong = WsFrameEncoder.Encode(WsOpcode.Pong, ping, randomSource);
            await WsProtocolHandler.SendAsync(connection, pong, cancellationToken).ConfigureAwait(false);
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
