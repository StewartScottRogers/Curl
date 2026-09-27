using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Writes a request body to the connection after its head, as curl 8.21.0 sends it: the
/// bytes as they are, or in chunks of chunked transfer coding with lower-case hexadecimal
/// sizes and a closing <c>0</c> chunk (BL-175 Notes).
/// </summary>
/// <remarks>
/// A <see cref="StreamBody" /> is read <see cref="ReadSize" /> bytes at a time, and each
/// read becomes one chunk when the body is chunked. A stream of known length is read up to
/// that length and no further; if a read fails or the stream ends first, the transfer ends
/// with exit 26. A stream of unknown length ends at its end or at its first failed read, as
/// curl's body reader treats both.
/// </remarks>
/// <param name="connection">The connection the request head was written to.</param>
internal sealed class HttpRequestBodyWriter(IConnection connection)
{
    /// <summary>
    /// The most bytes read from a <see cref="StreamBody" /> at a time: curl's 64 KiB upload
    /// buffer.
    /// </summary>
    internal const int ReadSize = 65536;

    private static readonly byte[] LastChunk = "0\r\n\r\n"u8.ToArray();

    /// <summary>
    /// Gets how many body bytes have been written, chunk framing excluded.
    /// </summary>
    internal long BytesWritten { get; private set; }

    /// <summary>
    /// Writes <paramref name="body" /> and flushes the connection.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <param name="isChunked">Whether the body is sent with chunked transfer coding.</param>
    /// <param name="cancellationToken">Cancels the writes and reads.</param>
    /// <returns>A task that completes when the body has been written.</returns>
    /// <exception cref="HttpTransferException">
    /// A stream of known length failed a read or ended before its length was read (exit 26),
    /// or the connection failed a write (exit 55).
    /// </exception>
    internal async ValueTask WriteAsync(HttpRequestBody body, bool isChunked, CancellationToken cancellationToken)
    {
        if (body is BytesBody bytes)
        {
            await WritePieceAsync(bytes.Content, isChunked, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await WriteStreamAsync((StreamBody)body, isChunked, cancellationToken).ConfigureAwait(false);
        }

        if (isChunked)
        {
            await HttpConnectionSend.WriteAsync(connection, LastChunk, cancellationToken).ConfigureAwait(false);
        }

        await HttpConnectionSend.FlushAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask WriteStreamAsync(StreamBody body, bool isChunked, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[ReadSize];
        while (true)
        {
            long remaining = body.Length is { } length ? length - BytesWritten : ReadSize;
            if (remaining == 0)
            {
                return;
            }

            int read = await ReadAsync(body.Content, buffer.AsMemory(0, (int)Math.Min(remaining, ReadSize)), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                ThrowIfShort(body.Length);
                return;
            }

            await WritePieceAsync(buffer.AsMemory(0, read), isChunked, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads from the body stream, taking a failed read as the end of the stream, as curl
    /// does.
    /// </summary>
    private static async ValueTask<int> ReadAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        try
        {
            return await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return 0;
        }
    }

    private void ThrowIfShort(long? length)
    {
        if (length is { } needed)
        {
            throw new HttpTransferException(CurlExitCode.ReadError, HttpTransferMessages.BodyStreamEndedEarly(BytesWritten, needed));
        }
    }

    private async ValueTask WritePieceAsync(ReadOnlyMemory<byte> piece, bool isChunked, CancellationToken cancellationToken)
    {
        if (piece.IsEmpty)
        {
            return;
        }

        if (isChunked)
        {
            byte[] size = Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{piece.Length:x}\r\n"));
            await HttpConnectionSend.WriteAsync(connection, size, cancellationToken).ConfigureAwait(false);
        }

        await HttpConnectionSend.WriteAsync(connection, piece, cancellationToken).ConfigureAwait(false);
        if (isChunked)
        {
            await HttpConnectionSend.WriteAsync(connection, "\r\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);
        }

        BytesWritten += piece.Length;
    }
}
