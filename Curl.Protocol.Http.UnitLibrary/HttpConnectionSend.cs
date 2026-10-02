using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Writes request bytes to a connection and flushes it, ending the transfer with exit 55 and
/// the message curl 8.21.0 prints when the connection fails the write or the flush (measured,
/// BL-174 Notes).
/// </summary>
internal static class HttpConnectionSend
{
    /// <summary>
    /// Writes <paramref name="bytes" /> to <paramref name="connection" />.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="bytes">The bytes to send.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the bytes are handed to the connection.</returns>
    /// <exception cref="HttpTransferException">The connection failed the write (exit 55).</exception>
    internal static async ValueTask WriteAsync(IConnection connection, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        try
        {
            await connection.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            throw SendFailed(exception);
        }
    }

    /// <summary>
    /// Flushes <paramref name="connection" />.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="cancellationToken">Cancels the flush.</param>
    /// <returns>A task that completes when the connection has drained.</returns>
    /// <exception cref="HttpTransferException">The connection failed the flush (exit 55).</exception>
    internal static async ValueTask FlushAsync(IConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            throw SendFailed(exception);
        }
    }

    /// <summary>
    /// Ends the request's HTTP/2 or HTTP/3 stream once its body has been written, when
    /// <paramref name="connection" /> is one (<see cref="IHttpStreamConnection.EndRequestAsync" />);
    /// does nothing for an HTTP/1.x connection, where the head and body frame the request.
    /// </summary>
    /// <param name="connection">The connection the request was sent on.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the stream's end is sent.</returns>
    /// <exception cref="HttpTransferException">The connection failed the write (exit 55).</exception>
    internal static async ValueTask EndStreamRequestAsync(IConnection connection, CancellationToken cancellationToken)
    {
        if (connection is not IHttpStreamConnection stream)
        {
            return;
        }

        try
        {
            await stream.EndRequestAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            throw SendFailed(exception);
        }
    }

    // A TLS handshake deferred to the first write (--tls-earlydata, BL-1105) fails as the
    // connect would have; any other failed write is exit 55.
    private static HttpTransferException SendFailed(IOException exception) =>
        exception is DeferredTlsHandshakeFailedException handshake
            ? new(handshake.ExitCode, handshake.Message)
            : new(CurlExitCode.SendError, HttpTransferMessages.SendFailure(exception));
}
