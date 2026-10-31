namespace Curl.Extension.Protocol.GoogleCloudStorage.Http;

/// <summary>A body of exactly <c>Content-Length</c> bytes (RFC 9112 §6.3, rule 6).</summary>
/// <param name="reader">The connection's reader.</param>
/// <param name="length">The number of bytes in the body.</param>
internal sealed class GcsContentLengthBodyStream(GcsHttpConnectionReader reader, long length) : GcsHttpBodyStream
{
    private long remaining = length;

    /// <inheritdoc/>
    protected override async ValueTask<int> ReadBodyAsync(Memory<byte> buffer, CancellationToken cancellationToken)
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
