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

    private static HttpTransferException SendFailed(IOException exception) =>
        new(CurlExitCode.SendError, HttpTransferMessages.SendFailure(exception));
}
