namespace Curl.Extended.Protocol.McpClient.StreamableHttp;

/// <summary>A response body read forward only; each framing (RFC 9112 §6.3) supplies <see cref="ReadAsync"/>.</summary>
internal abstract class McpHttpBody
{
    /// <summary>Reads the next bytes of the body.</summary>
    /// <param name="buffer">Where the bytes go; must not be empty.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The number of bytes read, or zero at the body's end.</returns>
    public abstract ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken);

    /// <summary>Reads the rest of the body.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bytes.</returns>
    public async Task<byte[]> ReadToEndAsync(CancellationToken cancellationToken)
    {
        var body = new MemoryStream();
        var buffer = new byte[16384];
        for (var read = await ReadAsync(buffer, cancellationToken); read > 0; read = await ReadAsync(buffer, cancellationToken))
        {
            body.Write(buffer, 0, read);
        }

        return body.ToArray();
    }

    /// <summary>Creates the failure for a connection that closed before the body's framing ended.</summary>
    /// <returns>The failure to throw.</returns>
    protected static McpHttpException ClosedEarly() =>
        new(McpHttpFailure.ClosedEarly, "The connection closed before the response body ended.");
}
