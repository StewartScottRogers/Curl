using Curl.Mcp.Protocol.JsonRpc;

namespace Curl.Mcp.Protocol.Transport;

/// <summary>
/// Carries JSON-RPC 2.0 messages between the client and the server: reads the client's messages one at a
/// time and writes the server's, whole, from any number of concurrent writers.
/// </summary>
public interface IMcpTransport
{
    /// <summary>Reads the client's next message. Not safe to call again before the previous call completes.</summary>
    /// <param name="cancellationToken">Cancels a pending read with an <see cref="OperationCanceledException" />.</param>
    /// <returns>
    /// The next request, notification or invalid message, or <see langword="null" /> at the end of the input.
    /// </returns>
    ValueTask<JsonRpcIncoming?> ReadMessageAsync(CancellationToken cancellationToken);

    /// <summary>Writes one message and flushes it. Safe to call from several tasks at once.</summary>
    /// <param name="message">The message's UTF-8 JSON, without a line terminator; it must not contain a newline.</param>
    /// <param name="cancellationToken">Cancels the write with an <see cref="OperationCanceledException" />.</param>
    /// <returns>A task that completes once the message is written and flushed.</returns>
    ValueTask WriteMessageAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken);
}
