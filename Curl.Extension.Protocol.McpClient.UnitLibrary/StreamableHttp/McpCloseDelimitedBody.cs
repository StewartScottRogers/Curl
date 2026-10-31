namespace Curl.Extension.Protocol.McpClient.StreamableHttp;

/// <summary>A body that ends when the server closes the connection (RFC 9112 §6.3, rule 8).</summary>
/// <param name="reader">The connection's reader.</param>
internal sealed class McpCloseDelimitedBody(McpHttpConnectionReader reader) : McpHttpBody
{
    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        reader.ReadAsync(buffer, cancellationToken);
}
