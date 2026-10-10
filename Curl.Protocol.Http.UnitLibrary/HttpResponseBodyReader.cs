using System.Runtime.CompilerServices;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Reads an HTTP/1.x response body framed by chunked transfer coding, by Content-Length or by
/// the peer closing (<see cref="HttpResponseBodyFraming" />), and writes it to the transfer's output, failing with the exit code and
/// message curl 8.21.0 reports (measured, BL-170, BL-171).
/// </summary>
/// <remarks>
/// The bytes read along with the head are written first, as one write; after them each
/// read of at most <see cref="ReceiveSize" /> bytes is written as it arrives, in writes of at most
/// <see cref="WriteSize" /> bytes, so a
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
    /// The most bytes one write passes to the output: curl 8.21.0's largest client write, the
    /// <c>passed 16384</c> it reports when the output fails on a large body (measured).
    /// </summary>
    internal const int WriteSize = 16384;

    /// <summary>
    /// The most bytes one read of a Content-Length or read-to-close body asks for: the
    /// 100 KiB receive buffer the curl tool sets (<c>CURLOPT_BUFFERSIZE</c>), so a large
    /// download takes a sixth of the reads <see cref="WriteSize" /> would (AF-0144, BL-1963).
    /// </summary>
    internal const int ReceiveSize = 102400;

    private HttpChunkedDecoder? decoder;

    private HttpContentDecoder? contentDecoder;

    /// <summary>
    /// Gets how many body bytes the output has accepted, whole writes only: after a failure
    /// it is the count that reached the output before the write that failed.
    /// </summary>
    internal long BytesWritten { get; private set; }

    /// <summary>
    /// Gets how many bytes the output has accepted after content decoding, curl's
    /// <c>%{size_delivered}</c>: <see cref="BytesWritten" /> when nothing is decoded, and the
    /// decoded count otherwise, so a gzip body of 51 bytes that decodes to 501 delivers 501
    /// (measured, BL-516 Notes).
    /// </summary>
    internal long BytesDelivered => contentDecoder?.BytesDelivered ?? BytesWritten;

    /// <summary>
    /// Gets a chunked body's trailer lines, each ending in a carriage return and line feed,
    /// which curl 8.21.0 writes to header output after the body; empty for any other body.
    /// After a failure it holds the trailer lines decoded before it.
    /// </summary>
    internal ReadOnlyMemory<byte> TrailerBytes => decoder?.TrailerBytes ?? ReadOnlyMemory<byte>.Empty;

    /// <summary>
    /// Gets or sets how many response headers the transfer has stored before a chunked body's
    /// trailers, which leave the trailers what is left of
    /// <see cref="HttpResponseHeadReader.MaximumHeaderCount" /> (<see cref="HttpChunkedDecoder.TrailerLimit" />,
    /// measured, BL-1448 Notes). 0 by default.
    /// </summary>
    internal int HeadersStoredBefore { get; set; }

    /// <summary>
    /// Gets how many response headers the transfer has stored: <see cref="HeadersStoredBefore" />
    /// and the chunked body's trailers decoded so far.
    /// </summary>
    internal int HeadersStored => HeadersStoredBefore + (decoder?.TrailerCount ?? 0);

    /// <summary>
    /// Gets or sets a value indicating whether <c>--max-filesize</c> was given, so a
    /// Content-Length too large for a signed 64-bit integer is refused with exit 63 as the head
    /// is read (<see cref="FindHeadRefusal" />), as curl 8.21.0 does whether or not the body is
    /// kept (<c>lib/http.c</c>, measured, BL-1387).
    /// </summary>
    internal bool LimitsFileSize { get; set; }

    /// <summary>
    /// Gets or sets the most body bytes the output may be given, <c>--max-filesize</c>'s limit,
    /// or <see langword="null" /> for no limit. A body that grows past it has as many bytes
    /// written as the limit allows, then fails with exit 63.
    /// </summary>
    internal long? MaximumBodySize { get; set; }

    /// <summary>
    /// Gets or sets the exchange's diagnostic log, told how the body is framed and which codings
    /// it decodes (BL-922); <see cref="HttpExchangeLog.Silent" /> unless an exchange sets one.
    /// </summary>
    internal HttpExchangeLog Log { get; set; } = HttpExchangeLog.Silent;

    /// <summary>
    /// Gets or sets a value indicating whether transfer coding is written undecoded, as
    /// <c>--raw</c> asks (<see cref="HttpResponseBodyFraming" />).
    /// </summary>
    internal bool PassesTransferCoding { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the Transfer-Encoding is read as
    /// <c>--tr-encoding</c> asks (<see cref="HttpResponseBodyFraming" />).
    /// </summary>
    internal bool DecodesTransferCoding { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the Content-Length is ignored and a body that is
    /// not chunked read until the peer closes, as <c>--ignore-content-length</c> asks.
    /// </summary>
    internal bool IgnoresContentLength { get; set; }

    /// <summary>
    /// Gets the Content-Length of the body being read, once reading has started and the body
    /// is framed by one rather than by chunked coding or the peer closing; otherwise
    /// <see langword="null" />. It is the <c>out of</c> size of curl's exit 28 message.
    /// </summary>
    internal long? ExpectedLength { get; private set; }

    /// <summary>
    /// Gets or sets where the body bytes the output has accepted are reported, with
    /// <see cref="ExpectedLength" /> as the expected total: once when reading starts and after
    /// each write.
    /// </summary>
    internal HttpTransferProgress Progress { get; set; } = HttpTransferProgress.Silent;

    /// <summary>
    /// Gets or sets where the body bytes are reported as they are received, before any decoding
    /// (ADR-0046): the bytes read along with the head as one
    /// <see cref="ITransferEvents.ReportDataReceived" />, then one per read, a chunked body's
    /// framing and trailers included, as curl 8.21.0's <c>--trace</c> shows them (measured,
    /// BL-407 Notes), and one empty event for a read-to-close body that closed without a byte
    /// (BL-1763).
    /// </summary>
    internal ITransferEvents Events { get; set; } = NoTransferEvents.Instance;

    /// <summary>
    /// Determines whether a response carries a body: not for <c>-I</c>, and not for a
    /// 204 or 304 status, whatever its Content-Length says.
    /// </summary>
    /// <param name="head">The final response's head.</param>
    /// <param name="noBody">
    /// <see langword="true" /> for <c>-I</c>, whatever method <c>-X</c> names: curl 8.21.0
    /// reads no body for <c>-I -X GET</c>. A HEAD sent through <c>-X HEAD</c> alone is not
    /// one: curl still reads that response's body (measured, BL-176 Notes).
    /// </param>
    /// <returns><see langword="true" /> when the body should be read.</returns>
    internal static bool HasBody(HttpResponseHead head, bool noBody) =>
        !noBody && head.StatusLine.StatusCode is not (204 or 304);

    /// <summary>
    /// Finds the first header curl 8.21.0 refuses while it reads the head, in the order the
    /// headers arrived (measured, BL-364, BL-412 and BL-1387 Notes): an invalid Content-Length (exit 8),
    /// or, under <see cref="LimitsFileSize" />, one too large for a 64-bit integer (exit 63),
    /// unless <c>--ignore-content-length</c>; a Transfer-Encoding
    /// <see cref="HttpResponseBodyFraming.Of" /> refuses (exit 61), but not for <c>-I</c>; and,
    /// for <c>--compressed</c>, the Content-Encoding header that takes the codings past
    /// <see cref="HttpContentDecoder.MaximumCodings" /> (exit 61). Nothing is refused in a 204
    /// or 304. Curl refuses the header before <c>-f</c> and before a redirect is followed, and
    /// writes header output only up to it.
    /// </summary>
    /// <param name="head">The response's head.</param>
    /// <param name="noBody"><see langword="true" /> for <c>-I</c>.</param>
    /// <param name="decodeContent"><see langword="true" /> for <c>--compressed</c> without <c>--raw</c>.</param>
    /// <returns>The refused header and its failure, or <see langword="null" /> when none is refused.</returns>
    internal HttpHeadRefusal? FindHeadRefusal(HttpResponseHead head, bool noBody, bool decodeContent)
    {
        if (!HasBody(head, noBody: false))
        {
            return null;
        }

        int? codingIndex = decodeContent ? HttpContentDecoder.IndexPastCodingLimit(head.Headers) : null;
        int checkedCount = codingIndex ?? head.Headers.Count;
        if (FramingFailure(head.Headers, checkedCount, noBody) is not null)
        {
            return FirstFramingRefusal(head.Headers, checkedCount, noBody);
        }

        return codingIndex is { } index
            ? new HttpHeadRefusal(index, new HttpTransferException(CurlExitCode.BadContentEncoding, HttpTransferMessages.TooManyContentCodings))
            : null;
    }

    /// <summary>
    /// Finds the header whose framing refusal the first <paramref name="refusedCount" />
    /// headers carry. Each check reads headers in order and fails at the first it refuses, so
    /// once a run of headers is refused every longer one is too, and a binary search finds the
    /// shortest refused run without checking every one of a long head's runs.
    /// </summary>
    private HttpHeadRefusal FirstFramingRefusal(IReadOnlyList<HttpResponseHeader> headers, int refusedCount, bool noBody)
    {
        int low = 1;
        int high = refusedCount;
        while (low < high)
        {
            int middle = low + ((high - low) / 2);
            if (FramingFailure(headers, middle, noBody) is null)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return new HttpHeadRefusal(low - 1, FramingFailure(headers, low, noBody)!);
    }

    /// <summary>
    /// Checks the framing headers among the first <paramref name="count" /> headers: the
    /// Content-Length alone for <c>-I</c>, which reads no body and so refuses no
    /// Transfer-Encoding (measured, BL-412 Notes).
    /// </summary>
    /// <returns>The failure, or <see langword="null" /> when those headers are accepted.</returns>
    private HttpTransferException? FramingFailure(IReadOnlyList<HttpResponseHeader> headers, int count, bool noBody)
    {
        IReadOnlyList<HttpResponseHeader> checkedHeaders = [.. headers.Take(count)];
        try
        {
            if (noBody)
            {
                _ = IgnoresContentLength ? null : HttpContentLength.Find(checkedHeaders);
            }
            else
            {
                HttpResponseBodyFraming.Of(checkedHeaders, PassesTransferCoding, IgnoresContentLength, DecodesTransferCoding);
            }

            return RefusesOverflow(checkedHeaders)
                ? new HttpTransferException(CurlExitCode.FilesizeExceeded, HttpTransferMessages.MaximumFileSizeExceeded)
                : null;
        }
        catch (HttpTransferException failure)
        {
            return failure;
        }
    }

    /// <summary>
    /// Determines whether <see cref="LimitsFileSize" /> refuses a Content-Length among
    /// <paramref name="headers" /> that <see cref="HttpContentLength.Overflows" />, unless
    /// <c>--ignore-content-length</c> leaves it unread.
    /// </summary>
    private bool RefusesOverflow(IReadOnlyList<HttpResponseHeader> headers) =>
        LimitsFileSize && !IgnoresContentLength && HttpContentLength.Overflows(headers);

    /// <summary>
    /// Reads the body and writes it to <paramref name="output" />, or reads nothing when the
    /// response has none.
    /// </summary>
    /// <param name="head">The final response's head, whose body prefix is written first.</param>
    /// <param name="noBody">
    /// <see langword="true" /> for <c>-I</c>; see
    /// <see cref="HasBody(HttpResponseHead, bool)" />.
    /// </param>
    /// <param name="output">Where the body goes.</param>
    /// <param name="decodeContent">
    /// <see langword="true" /> for <c>--compressed</c> without <c>--raw</c>: the body is decoded
    /// as its Content-Encoding headers say (<see cref="HttpContentDecoder" />) before it is written,
    /// while <see cref="BytesWritten" /> still counts the encoded bytes, as curl's
    /// <c>%{size_download}</c> does, and <see cref="BytesDelivered" /> the decoded ones.
    /// </param>
    /// <param name="decodeTransfer">
    /// <see langword="true" /> for <c>--tr-encoding</c> when the body is not discarded: the
    /// Transfer-Encoding codings other than <c>chunked</c> are decoded, before any
    /// Content-Encoding, and <see cref="BytesWritten" /> still counts the encoded bytes.
    /// </param>
    /// <param name="cancellationToken">Cancels every read and write.</param>
    /// <returns>A task that completes when the whole body is written.</returns>
    /// <exception cref="HttpTransferException">
    /// The Content-Length or a chunked body's trailer is invalid (exit 8), the peer closed
    /// before the body was whole (exit 18), the output failed a write (exit 23), a read
    /// failed or the chunked framing is malformed (exit 56), the Transfer-Encoding names a
    /// coding curl does not decode or a decoded Content-Encoding is unrecognized or corrupt
    /// (exit 61), the body grew past <see cref="MaximumBodySize" /> (exit 63), or a trailer line
    /// is too long (exit 100).
    /// </exception>
    internal async ValueTask CopyAsync(
        HttpResponseHead head,
        bool noBody,
        Stream output,
        bool decodeContent,
        bool decodeTransfer,
        CancellationToken cancellationToken)
    {
        if (!HasBody(head, noBody))
        {
            return;
        }

        HttpResponseBodyFraming framing = HttpResponseBodyFraming.Of(head.Headers, PassesTransferCoding, IgnoresContentLength, DecodesTransferCoding);
        IEnumerable<string> contentCodings = decodeContent ? HttpContentDecoder.ContentCodings(head.Headers) : [];
        IEnumerable<string> transferCodings = decodeTransfer ? framing.TransferCodings : [];
        string[] decodedCodings = [.. contentCodings.Concat(transferCodings)];
        Log.BodyFramed(framing, decodedCodings);
        contentDecoder = HttpContentDecoder.ForCodings(decodedCodings);
        contentDecoder?.MaximumDeliveredSize = MaximumBodySize;
        ExpectedLength = framing.ContentLength;
        Progress.ReportDownloaded(BytesWritten, ExpectedLength);
        try
        {
            await CopyFramedAsync(head.BodyPrefix, framing.IsChunked, framing.ContentLength, output, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            contentDecoder?.Dispose();
        }
    }

    /// <summary>
    /// Reads the body as its framing says, starting with the bytes read along with the head:
    /// chunked, up to a Content-Length, or until the peer closes.
    /// </summary>
    private async ValueTask CopyFramedAsync(
        ReadOnlyMemory<byte> bodyPrefix,
        bool isChunked,
        long? contentLength,
        Stream output,
        CancellationToken cancellationToken)
    {
        if (isChunked)
        {
            await CopyChunkedAsync(bodyPrefix, output, cancellationToken).ConfigureAwait(false);
            return;
        }

        long remaining = contentLength ?? long.MaxValue;
        ReadOnlyMemory<byte> prefix = bodyPrefix[..(int)Math.Min(bodyPrefix.Length, remaining)];
        ReportReceived(prefix);
        remaining -= await WriteAsync(output, prefix, cancellationToken).ConfigureAwait(false);

        byte[] buffer = new byte[ReceiveSize];
        while (remaining > 0)
        {
            int read = await ReadAsync(buffer.AsMemory(0, (int)Math.Min(ReceiveSize, remaining)), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                EndAtClose(contentLength is null, remaining);
                return;
            }

            remaining -= await WriteInPiecesAsync(output, buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reports and writes one read's bytes in pieces of at most <see cref="WriteSize" />.
    /// </summary>
    /// <returns>How many bytes were written.</returns>
    private async ValueTask<int> WriteInPiecesAsync(Stream output, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        int written = 0;
        while (written < bytes.Length)
        {
            ReadOnlyMemory<byte> piece = bytes.Slice(written, Math.Min(WriteSize, bytes.Length - written));
            ReportReceived(piece);
            written += await WriteAsync(output, piece, cancellationToken).ConfigureAwait(false);
        }

        return written;
    }

    /// <summary>
    /// Decodes a chunked body, starting with the bytes read along with the head, and writes
    /// its data to <paramref name="output" /> until the empty line after its trailers.
    /// </summary>
    private async ValueTask CopyChunkedAsync(ReadOnlyMemory<byte> bytes, Stream output, CancellationToken cancellationToken)
    {
        decoder = new HttpChunkedDecoder { TrailerLimit = HttpResponseHeadReader.MaximumHeaderCount - HeadersStoredBefore };
        ReportReceived(bytes);
        byte[] buffer = new byte[WriteSize];
        while (true)
        {
            while (!bytes.IsEmpty && !decoder.IsComplete)
            {
                ReadOnlyMemory<byte> data = decoder.DecodeNext(bytes, out int consumed);
                bytes = bytes[consumed..];
                await WriteChunkDataAsync(output, data, cancellationToken).ConfigureAwait(false);
            }

            if (decoder.IsComplete)
            {
                ReportLeftovers(bytes);
                return;
            }

            int read = await ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new HttpTransferException(CurlExitCode.PartialFile, HttpTransferMessages.ChunkedBodyIncomplete);
            }

            bytes = buffer.AsMemory(0, read);
            ReportReceived(bytes);
        }
    }

    /// <summary>
    /// Reports <see cref="HttpConnectionInfoLines.LeftoversAfterChunking" /> for the bytes left
    /// in the read that completed a chunked body, or nothing when there are none.
    /// </summary>
    private void ReportLeftovers(ReadOnlyMemory<byte> leftovers)
    {
        if (!leftovers.IsEmpty)
        {
            Events.ReportInfo(HttpConnectionInfoLines.LeftoversAfterChunking(leftovers.Length));
        }
    }

    /// <summary>
    /// Reports received body bytes as one data event, or nothing when there are none.
    /// </summary>
    private void ReportReceived(ReadOnlyMemory<byte> bytes)
    {
        if (!bytes.IsEmpty)
        {
            Events.ReportDataReceived(bytes.Span);
        }
    }

    /// <summary>
    /// Writes one piece of chunk data. Bytes after the end of a decoded coding's stream fail
    /// with exit 23 and the chunked message, as curl 8.21.0 reports them inside chunks
    /// (measured, BL-365 Notes); every other failure keeps its own message.
    /// </summary>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask WriteChunkDataAsync(Stream output, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        try
        {
            await WriteAsync(output, data, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpTransferException exception) when (exception.Message == HttpTransferMessages.ReceivedDataWriteFailed)
        {
            throw new HttpTransferException(exception.ExitCode, HttpTransferMessages.ChunkedStreamReadFailed);
        }
    }

    /// <summary>
    /// Ends the body where the peer closed: cleanly for a read-to-close body, and with exit
    /// 18 for a Content-Length body still short. A read-to-close body that ended without a
    /// byte reports its closing read as one empty data event, which <c>-v</c> shows as
    /// <c>{ [0 bytes data]</c>, as curl 8.21.0 does (measured, BL-1763 Notes).
    /// </summary>
    private void EndAtClose(bool isReadToClose, long remaining)
    {
        if (!isReadToClose)
        {
            throw new HttpTransferException(CurlExitCode.PartialFile, HttpTransferMessages.BodyBytesMissing(remaining));
        }

        if (remaining == long.MaxValue)
        {
            Events.ReportDataReceived([]);
        }
    }

    private static HttpTransferException WriteFailed(int passed, int returned) =>
        new(CurlExitCode.WriteError, HttpTransferMessages.OutputWriteFailed(passed, returned));

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
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

    private ValueTask WriteDecodedAsync(Stream output, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) =>
        contentDecoder is null
            ? output.WriteAsync(bytes, cancellationToken)
            : contentDecoder.WriteAsync(output, bytes, cancellationToken);

    /// <summary>
    /// Gets how many more body bytes <see cref="MaximumBodySize" /> allows.
    /// </summary>
    private long RoomLeft => MaximumBodySize is { } limit ? limit - BytesWritten : long.MaxValue;

    /// <summary>
    /// Writes <paramref name="bytes" />, or as many as <see cref="MaximumBodySize" /> allows and
    /// then fails with exit 63.
    /// </summary>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<int> WriteAsync(Stream output, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        if (bytes.Length > RoomLeft)
        {
            await WriteWithinLimitAsync(output, bytes[..(int)RoomLeft], cancellationToken).ConfigureAwait(false);
            throw new HttpTransferException(
                CurlExitCode.FilesizeExceeded,
                HttpTransferMessages.FileSizeLimitExceeded(MaximumBodySize.GetValueOrDefault(), BytesWritten));
        }

        return await WriteWithinLimitAsync(output, bytes, cancellationToken).ConfigureAwait(false);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<int> WriteWithinLimitAsync(Stream output, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        if (bytes.IsEmpty)
        {
            return 0;
        }

        try
        {
            await WriteDecodedAsync(output, bytes, cancellationToken).ConfigureAwait(false);
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
        Progress.ReportDownloaded(BytesWritten, ExpectedLength);
        return bytes.Length;
    }
}
