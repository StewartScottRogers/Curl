using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Reads an HTTP/1.x response body framed by chunked transfer coding, by Content-Length or by
/// the peer closing, and writes it to the transfer's output, failing with the exit code and
/// message curl 8.21.0 reports (measured, BL-170, BL-171).
/// </summary>
/// <remarks>
/// The bytes read along with the head are written first, as one write; after them each
/// read is written as it arrives, in reads of at most <see cref="ReadSize" /> bytes, so a
/// failed write reports the same <c>passed</c> size curl does. A Content-Length body stops
/// at its length and ignores anything the peer sends past it. A chunked body wins over a
/// Content-Length, which is still checked; its data is written as it is decoded, each run of
/// chunk data within one read as one write, and its trailers are kept in
/// <see cref="TrailerBytes" /> for header output.
/// </remarks>
/// <param name="connection">The connection the response head was read from.</param>
internal sealed class HttpResponseBodyReader(IConnection connection)
{
    /// <summary>
    /// The most bytes one read asks for: curl 8.21.0's receive buffer, which is the
    /// <c>passed 16384</c> it reports when the output fails on a large body (measured).
    /// </summary>
    internal const int ReadSize = 16384;

    private HttpChunkedDecoder? decoder;

    /// <summary>
    /// Gets how many body bytes the output has accepted, whole writes only: after a failure
    /// it is the count that reached the output before the write that failed.
    /// </summary>
    internal long BytesWritten { get; private set; }

    /// <summary>
    /// Gets a chunked body's trailer lines, each ending in a carriage return and line feed,
    /// which curl 8.21.0 writes to header output after the body; empty for any other body.
    /// After a failure it holds the trailer lines decoded before it.
    /// </summary>
    internal ReadOnlyMemory<byte> TrailerBytes => decoder?.TrailerBytes ?? ReadOnlyMemory<byte>.Empty;

    /// <summary>
    /// Determines whether a response carries a body: not for a HEAD request, and not for a
    /// 204 or 304 status, whatever its Content-Length says.
    /// </summary>
    /// <param name="head">The final response's head.</param>
    /// <param name="isHeadRequest">
    /// <see langword="true" /> when curl sent HEAD for <c>-I</c>. A HEAD sent through
    /// <c>-X HEAD</c> is not one: curl 8.21.0 still reads that response's body (measured).
    /// </param>
    /// <returns><see langword="true" /> when the body should be read.</returns>
    internal static bool HasBody(HttpResponseHead head, bool isHeadRequest) =>
        !isHeadRequest && head.StatusLine.StatusCode is not (204 or 304);

    /// <summary>
    /// Reads the body and writes it to <paramref name="output" />, or reads nothing when the
    /// response has none.
    /// </summary>
    /// <param name="head">The final response's head, whose body prefix is written first.</param>
    /// <param name="isHeadRequest">
    /// <see langword="true" /> when curl sent HEAD for <c>-I</c>; see
    /// <see cref="HasBody(HttpResponseHead, bool)" />.
    /// </param>
    /// <param name="output">Where the body goes.</param>
    /// <param name="cancellationToken">Cancels every read and write.</param>
    /// <returns>A task that completes when the whole body is written.</returns>
    /// <exception cref="HttpTransferException">
    /// The Content-Length or a chunked body's trailer is invalid (exit 8), the peer closed
    /// before the body was whole (exit 18), the output failed a write (exit 23), a read
    /// failed or the chunked framing is malformed (exit 56), the Transfer-Encoding names a
    /// coding curl does not decode (exit 61), or a trailer line is too long (exit 100).
    /// </exception>
    internal async ValueTask CopyAsync(
        HttpResponseHead head,
        bool isHeadRequest,
        Stream output,
        CancellationToken cancellationToken)
    {
        if (!HasBody(head, isHeadRequest))
        {
            return;
        }

        bool isChunked = HttpTransferEncoding.IsChunked(head.Headers);
        long? contentLength = HttpContentLength.Find(head.Headers);
        if (isChunked)
        {
            await CopyChunkedAsync(head.BodyPrefix, output, cancellationToken).ConfigureAwait(false);
            return;
        }

        long remaining = contentLength ?? long.MaxValue;
        ReadOnlyMemory<byte> prefix = head.BodyPrefix[..(int)Math.Min(head.BodyPrefix.Length, remaining)];
        remaining -= await WriteAsync(output, prefix, cancellationToken).ConfigureAwait(false);

        byte[] buffer = new byte[ReadSize];
        while (remaining > 0)
        {
            int read = await ReadAsync(buffer.AsMemory(0, (int)Math.Min(ReadSize, remaining)), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                EndAtClose(contentLength is null, remaining);
                return;
            }

            remaining -= await WriteAsync(output, buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Decodes a chunked body, starting with the bytes read along with the head, and writes
    /// its data to <paramref name="output" /> until the empty line after its trailers.
    /// </summary>
    private async ValueTask CopyChunkedAsync(ReadOnlyMemory<byte> bytes, Stream output, CancellationToken cancellationToken)
    {
        decoder = new HttpChunkedDecoder();
        byte[] buffer = new byte[ReadSize];
        while (true)
        {
            while (!bytes.IsEmpty && !decoder.IsComplete)
            {
                ReadOnlyMemory<byte> data = decoder.DecodeNext(bytes, out int consumed);
                bytes = bytes[consumed..];
                await WriteAsync(output, data, cancellationToken).ConfigureAwait(false);
            }

            if (decoder.IsComplete)
            {
                return;
            }

            int read = await ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new HttpTransferException(CurlExitCode.PartialFile, HttpTransferMessages.ChunkedBodyIncomplete);
            }

            bytes = buffer.AsMemory(0, read);
        }
    }

    /// <summary>
    /// Ends the body where the peer closed: cleanly for a read-to-close body, and with exit
    /// 18 for a Content-Length body still short.
    /// </summary>
    private static void EndAtClose(bool isReadToClose, long remaining)
    {
        if (!isReadToClose)
        {
            throw new HttpTransferException(CurlExitCode.PartialFile, HttpTransferMessages.BodyBytesMissing(remaining));
        }
    }

    private static HttpTransferException WriteFailed(int passed, int returned) =>
        new(CurlExitCode.WriteError, HttpTransferMessages.OutputWriteFailed(passed, returned));

    private async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        try
        {
            return await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            throw new HttpTransferException(CurlExitCode.RecvError, HttpTransferMessages.ReceiveFailure(exception));
        }
    }

    private async ValueTask<int> WriteAsync(Stream output, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        if (bytes.IsEmpty)
        {
            return 0;
        }

        try
        {
            await output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        }
        catch (OutputWriteFailedException failure)
        {
            throw WriteFailed(bytes.Length, failure.BytesAccepted);
        }
        catch (IOException)
        {
            throw WriteFailed(bytes.Length, 0);
        }

        BytesWritten += bytes.Length;
        return bytes.Length;
    }
}
