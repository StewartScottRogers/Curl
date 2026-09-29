namespace Curl.Protocol.Ws.Fakes;

/// <summary>
/// Runs a <see cref="WsFrameReceiver" /> through a whole transfer with no upload between its
/// two steps: the bytes that came with the head, then reads until the server closes.
/// </summary>
internal static class WsFrameReceiverSequence
{
    /// <summary>Delivers <paramref name="alreadyReceived" />, then reads until the connection closes.</summary>
    /// <param name="receiver">The receiver under test.</param>
    /// <param name="alreadyReceived">The bytes that arrived with the upgrade reply's head.</param>
    /// <param name="writePayload">Receives each run of payload bytes.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>A task that completes once the server has closed the connection.</returns>
    internal static async Task ReceiveAsync(
        this WsFrameReceiver receiver,
        ReadOnlyMemory<byte> alreadyReceived,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> writePayload,
        CancellationToken cancellationToken)
    {
        await receiver.DeliverAlreadyReceivedAsync(alreadyReceived, writePayload, cancellationToken);
        await receiver.ReceiveUntilClosedAsync(writePayload, cancellationToken);
    }
}
