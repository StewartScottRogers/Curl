using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Reads an HTTP/1.x response body framed by Content-Length or by the peer closing, and
/// writes it to the transfer's output, failing with the exit code and message curl 8.21.0
/// reports (measured, BL-170).
/// </summary>
/// <remarks>
/// The bytes read along with the head are written first, as one write; after them each
/// read is written as it arrives, in reads of at most <see cref="ReadSize" /> bytes, so a
/// failed write reports the same <c>passed</c> size curl does. A Content-Length body stops
/// at its length and ignores anything the peer sends past it. A chunked body is not read
/// here.
/// </remarks>
/// <param name="connection">The connection the response head was read from.</param>
internal sealed class HttpResponseBodyReader(IConnection connection)
{
    /// <summary>
    /// The most bytes one read asks for: curl 8.21.0's receive buffer, which is the
    /// <c>passed 16384</c> it reports when the output fails on a large body (measured).
    /// </summary>
    internal const int ReadSize = 16384;

    /// <summary>
    /// Gets how many body bytes the output has accepted, whole writes only: after a failure
    /// it is the count that reached the output before the write that failed.
    /// </summary>
    internal long BytesWritten { get; private set; }

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
    /// The Content-Length is invalid (exit 8), the peer closed before the Content-Length
    /// body was whole (exit 18), the output failed a write (exit 23), or a read failed
    /// (exit 56).
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

        long? contentLength = HttpContentLength.Find(head.Headers);
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
