using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Stands in for the HTTP/1.x connection of a request whose body is a stream sent without waiting
/// for <c>100 Continue</c>: while a read of the body stays pending, as a libcurl read callback's
/// <c>CURL_READFUNC_PAUSE</c> keeps it, <see cref="ReadStatusCodeAsync" /> reads the response's status
/// line, so the writer sees a server that answered before the body was sent (upstream case 1533,
/// BL-2729). Every byte that read took is replayed before the connection is read again. Disposing
/// this does nothing: the connection it wraps belongs to the handler.
/// </summary>
/// <param name="connection">The connection the request head is written to.</param>
internal sealed class HttpEarlyResponseConnection(IConnection connection) : IConnection
{
    private readonly byte[] received = new byte[HttpLineReader.MaximumLineLength];

    private int receivedLength;

    private int replayed;

    private Task<int?>? statusLineRead;

    /// <inheritdoc />
    public bool IsSecure => connection.IsSecure;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => connection.RemoteEndPoint;

    /// <summary>
    /// Reads the response's first line, once: the first call starts the read and every call gives it.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read the first call starts.</param>
    /// <returns>
    /// The status code of the first line, or <see langword="null" /> when the connection ended, the
    /// line filled the buffer, or it is not a status line.
    /// </returns>
    internal Task<int?> ReadStatusCodeAsync(CancellationToken cancellationToken) =>
        statusLineRead ??= ReadStatusLineAsync(cancellationToken);

    /// <inheritdoc />
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (statusLineRead is not null)
        {
            await statusLineRead.ConfigureAwait(false);
        }

        if (replayed == receivedLength)
        {
            return await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        int length = Math.Min(buffer.Length, receivedLength - replayed);
        received.AsSpan(replayed, length).CopyTo(buffer.Span);
        replayed += length;
        return length;
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        connection.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => connection.FlushAsync(cancellationToken);

    /// <summary>
    /// Does nothing: the wrapped connection is disposed by whoever opened it.
    /// </summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Reads until a line feed, the end of the connection, or a full buffer, and gives the first
    /// line's status code.
    /// </summary>
    private async Task<int?> ReadStatusLineAsync(CancellationToken cancellationToken)
    {
        int lineFeed;
        while ((lineFeed = Array.IndexOf(received, (byte)'\n', 0, receivedLength)) < 0 && receivedLength < received.Length)
        {
            int read = await connection.ReadAsync(received.AsMemory(receivedLength), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            receivedLength += read;
        }

        return lineFeed >= 0 ? HttpContinueWaitConnection.StatusCodeOf(received.AsSpan(0, lineFeed + 1)) : null;
    }
}
