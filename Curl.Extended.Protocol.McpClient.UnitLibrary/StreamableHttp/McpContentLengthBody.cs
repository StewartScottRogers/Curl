namespace Curl.Extended.Protocol.McpClient.StreamableHttp;

/// <summary>A body of exactly <c>Content-Length</c> bytes (RFC 9112 §6.3, rule 6).</summary>
/// <param name="reader">The connection's reader.</param>
/// <param name="length">The number of bytes in the body.</param>
internal sealed class McpContentLengthBody(McpHttpConnectionReader reader, long length) : McpHttpBody
{
    private long remaining = length;

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (remaining == 0)
        {
            return 0;
        }

        var read = await reader.ReadAsync(buffer[..(int)Math.Min(buffer.Length, remaining)], cancellationToken);
        if (read == 0)
        {
            throw ClosedEarly();
        }

        remaining -= read;
        return read;
    }
}
