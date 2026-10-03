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
/// A <see cref="StreamBody" /> is read into curl's <see cref="UploadBufferSize" />-byte upload
/// buffer, and each read becomes one chunk when the body is chunked. The first read shares
/// the buffer with the request head (<see cref="SharedHeadLength" />), and a chunked read
/// leaves <see cref="ChunkFramingReserve" /> bytes of it for the chunk's framing, so a body
/// is read in the same pieces curl reads it in (measured, BL-184 Notes). A stream of known
/// length is read up to that length and no further; if a read fails or the stream ends
/// first, the transfer ends with exit 26. A stream of unknown length ends at its end or at
/// its first failed read, as curl's body reader treats both. A read that throws
/// <see cref="RequestBodyReadFailedException" /> is the exception: it fails the transfer with
/// exit 26 and the exception's message, and a chunked body gets no closing chunk. Under
/// <see cref="EarlyResponseWatch" /> every piece, a <see cref="BytesBody" /> cut into the same
/// buffer-sized pieces, is raced against a status of 300 or above, and sending stops at the first piece that
/// status beats (<see cref="CutShort" />, measured, BL-319 and BL-395 Notes).
/// </remarks>
/// <param name="connection">The connection the request head was written to.</param>
internal sealed class HttpRequestBodyWriter(IConnection connection)
{
    /// <summary>
    /// The size of curl's upload buffer, 64 KiB: the most bytes read from a
    /// <see cref="StreamBody" /> at a time.
    /// </summary>
    internal const int UploadBufferSize = 65536;

    /// <summary>
    /// The bytes of the upload buffer curl keeps back from each chunked read for the chunk's
    /// size line and closing CRLF: a stdin upload is sent in chunks of 65524 bytes (measured,
    /// BL-184 Notes).
    /// </summary>
    internal const int ChunkFramingReserve = 12;

    private bool headSent;

    private static readonly byte[] LastChunk = "0\r\n\r\n"u8.ToArray();

    /// <summary>
    /// Gets how many body bytes have been written, chunk framing excluded.
    /// </summary>
    internal long BytesWritten { get; private set; }

    /// <summary>
    /// Gets how many body bytes have gone onto the connection, chunk framing and the closing
    /// chunk included: the count curl 8.21.0 prints as <c>upload completely sent off: N bytes</c>
    /// (measured, BL-407 Notes).
    /// </summary>
    internal long BytesSent { get; private set; }

    /// <summary>
    /// Gets where the head and body bytes are reported as they are sent (ADR-0046): the head as
    /// one <see cref="ITransferEvents.ReportRequestHeader" />, and each piece of the body, and
    /// the closing chunk, as one <see cref="ITransferEvents.ReportDataSent" /> with its chunk
    /// framing, as curl 8.21.0's <c>--trace</c> shows them (measured, BL-407 Notes).
    /// </summary>
    internal ITransferEvents Events { get; init; } = NoTransferEvents.Instance;

    /// <summary>
    /// Gets the length of the request head that shares the upload buffer with the first read:
    /// the whole head when the body follows it at once, 0 when the head went out alone to wait
    /// for <c>100 Continue</c>. A head that fills the buffer leaves the first read a whole one.
    /// </summary>
    internal int SharedHeadLength { get; init; }

    /// <summary>
    /// Gets the request head this writer sends, written and flushed by
    /// <see cref="WriteHeldHeadAsync" /> or just before the first body bytes, so a body whose
    /// first read fails sends no request bytes at all, as curl 8.21.0 holds the head in its
    /// upload buffer with the first read (measured, BL-184 Notes). Empty when the head was
    /// sent elsewhere.
    /// </summary>
    internal ReadOnlyMemory<byte> HeldHead
    {
        get => heldHead;
        init => heldHead = value;
    }

    private ReadOnlyMemory<byte> heldHead;

    /// <summary>
    /// Gets a value indicating whether the body is a <c>-T</c> upload, whose short read curl
    /// reports as <c>client read function EOF fail</c> rather than
    /// <c>client mime read EOF fail</c> (measured, BL-184 Notes).
    /// </summary>
    internal bool IsUpload { get; init; }

    /// <summary>
    /// Gets where the body bytes sent so far are reported, with the body's length when it is
    /// known: once before the first byte and after each piece sent.
    /// </summary>
    internal HttpTransferProgress Progress { get; init; } = HttpTransferProgress.Silent;

    private long? expectedLength;

    private long? startPosition;

    private ReadOnlyMemory<byte> unsent;

    /// <summary>
    /// Gets the connection that waited for <c>100 Continue</c> and so watches for a
    /// status of 300 or above while the body is sent, or <see langword="null" /> when the
    /// request waited for nothing and the body is sent whole.
    /// </summary>
    internal HttpContinueWaitConnection? EarlyResponseWatch { get; init; }

    /// <summary>
    /// Gets a value indicating whether sending stopped before the end of the body because a
    /// status of 300 or above arrived first.
    /// </summary>
    internal bool CutShort { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the body's reads are reported as curl 8.21.0's
    /// <c>--trace-config read</c> lines (<see cref="HttpClientReaderTraceLines" />, BL-1189, BL-1214):
    /// bodies of bytes, <c>-T</c> uploads and multipart bodies, chunked or not, sent at once or after
    /// the wait for <c>100 Continue</c>. A traced body of bytes is sent in the pieces curl's upload
    /// buffer reads it in.
    /// </summary>
    internal bool TracesReaders { get; init; }

    private bool readerReported;

    private bool tracing;

    /// <summary>
    /// Writes <paramref name="body" /> and flushes the connection, then, when the connection is
    /// an HTTP/2 or HTTP/3 stream, ends the stream unless the last write already did
    /// (<see cref="HttpConnectionSend.EndStreamRequestAsync" />).
    /// </summary>
    /// <param name="body">The body.</param>
    /// <param name="isChunked">Whether the body is sent with chunked transfer coding.</param>
    /// <param name="cancellationToken">Cancels the writes and reads.</param>
    /// <returns>A task that completes when the body has been written.</returns>
    /// <exception cref="HttpTransferException">
    /// A stream of known length failed a read or ended before its length was read (exit 26),
    /// a read threw <see cref="RequestBodyReadFailedException" /> (exit 26), or the connection
    /// failed a write (exit 55).
    /// </exception>
    internal async ValueTask WriteAsync(HttpRequestBody body, bool isChunked, CancellationToken cancellationToken)
    {
        expectedLength = body is BytesBody known ? known.Content.Length : ((StreamBody)body).Length;
        Progress.ReportUploaded(BytesWritten, expectedLength);
        ReportReaderAdded(body);
        if (body is BytesBody bytes)
        {
            await WriteBytesAsync(bytes.Content, isChunked, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await WriteStreamAsync((StreamBody)body, isChunked, cancellationToken).ConfigureAwait(false);
        }

        await WriteHeldHeadAsync(cancellationToken).ConfigureAwait(false);
        if (isChunked && !CutShort)
        {
            await HttpConnectionSend.WriteAsync(connection, LastChunk, cancellationToken).ConfigureAwait(false);
            ReportSent(LastChunk);
        }

        await HttpConnectionSend.FlushAsync(connection, cancellationToken).ConfigureAwait(false);
        await HttpConnectionSend.EndStreamRequestAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes and flushes <see cref="HeldHead" /> if it has not been sent yet; does nothing
    /// after that.
    /// </summary>
    /// <param name="cancellationToken">Cancels the write and the flush.</param>
    /// <returns>A task that completes when the head has been sent.</returns>
    /// <exception cref="HttpTransferException">The connection failed the write (exit 55).</exception>
    internal async ValueTask WriteHeldHeadAsync(CancellationToken cancellationToken)
    {
        if (heldHead.IsEmpty)
        {
            return;
        }

        ReadOnlyMemory<byte> head = heldHead;
        heldHead = ReadOnlyMemory<byte>.Empty;
        await HttpConnectionSend.WriteAsync(connection, head, cancellationToken).ConfigureAwait(false);
        Events.ReportRequestHeader(head.Span);
        await HttpConnectionSend.FlushAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gives the body a resend of this request sends: the same bytes, or the same stream
    /// rewound to where this writer began reading it; a stream that cannot seek goes on from
    /// where it is, after the bytes of a piece a <c>417</c> cut short, as curl 8.21.0 goes on
    /// reading stdin rather than failing (measured, BL-319 Notes).
    /// </summary>
    /// <param name="body">The body this writer was given.</param>
    /// <returns>The body to resend.</returns>
    internal HttpRequestBody Rewound(HttpRequestBody body)
    {
        if (body is not StreamBody stream)
        {
            return body;
        }

        if (startPosition is { } start)
        {
            stream.Content.Position = start;
            return body;
        }

        return stream with { Content = new HttpPrefixedStream(unsent, stream.Content) };
    }

    /// <summary>
    /// Writes a body of bytes: whole, or under <see cref="EarlyResponseWatch" /> in the pieces
    /// curl's upload buffer holds, until they are all sent or one is cut short.
    /// </summary>
    private async ValueTask WriteBytesAsync(ReadOnlyMemory<byte> content, bool isChunked, CancellationToken cancellationToken)
    {
        if (EarlyResponseWatch is null && !tracing)
        {
            await WritePieceAsync(content, isChunked, cancellationToken).ConfigureAwait(false);
            return;
        }

        while (BytesWritten < content.Length && !CutShort)
        {
            int room = NextReadRoom(isChunked);
            int length = (int)Math.Min(room, content.Length - BytesWritten);
            ReportBufferRead(room, length, BytesWritten + length == content.Length, isChunked);
            await WritePieceAsync(content.Slice((int)BytesWritten, length), isChunked, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask WriteStreamAsync(StreamBody body, bool isChunked, CancellationToken cancellationToken)
    {
        startPosition = body.Content.CanSeek ? body.Content.Position : null;
        byte[] buffer = new byte[UploadBufferSize];
        while (!CutShort)
        {
            int room = NextReadRoom(isChunked);
            long remaining = body.Length is { } length ? length - BytesWritten : room;
            if (remaining == 0)
            {
                return;
            }

            int requested = (int)Math.Min(remaining, room);
            int read = await ReadAsync(body.Content, buffer.AsMemory(0, requested), cancellationToken)
                .ConfigureAwait(false);
            ReportStreamRead(room, requested, body.Length, read, isChunked);
            if (read == 0)
            {
                ThrowIfShort(body.Length);
                return;
            }

            await WritePieceAsync(buffer.AsMemory(0, read), isChunked, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sends <see cref="HeldHead" /> alone before the wait for <c>100 Continue</c>, between the two
    /// reads curl 8.21.0 makes while the body is held back, each of which reads nothing: the first
    /// with the room the head leaves in the upload buffer, the second with the whole buffer (measured,
    /// BL-1214 Notes). Under <see cref="TracesReaders" /> the body's reader and both reads are reported.
    /// </summary>
    /// <param name="body">The body that waits.</param>
    /// <param name="cancellationToken">Cancels the write and the flush.</param>
    /// <returns>A task that completes when the head has been sent.</returns>
    /// <exception cref="HttpTransferException">The connection failed the write (exit 55).</exception>
    internal async ValueTask WriteHeadBeforeContinueAsync(HttpRequestBody body, CancellationToken cancellationToken)
    {
        ReportReaderAdded(body);
        ReportHeldBackRead(UploadBufferSize - heldHead.Length);
        await WriteHeldHeadAsync(cancellationToken).ConfigureAwait(false);
        ReportHeldBackRead(UploadBufferSize);
    }

    /// <summary>
    /// Reports, once, the reader curl adds for the body under <see cref="TracesReaders" />, and from
    /// then on traces its reads; nothing for a body <see cref="HttpClientReaderTraceLines.IsRead" />
    /// refuses, and no line for one <see cref="HttpClientReaderTraceLines.AddReader" /> gives none for.
    /// </summary>
    private void ReportReaderAdded(HttpRequestBody body)
    {
        if (!TracesReaders || readerReported)
        {
            return;
        }

        readerReported = true;
        tracing = HttpClientReaderTraceLines.IsRead(body);
        if (tracing && HttpClientReaderTraceLines.AddReader(body, IsUpload) is { } line)
        {
            Events.ReportInfo(line);
        }
    }

    /// <summary>Reports one read of a traced body held back for <c>100 Continue</c>, which reads nothing.</summary>
    private void ReportHeldBackRead(int room)
    {
        if (tracing)
        {
            Events.ReportInfo(HttpClientReaderTraceLines.ClientRead(room, 0, false));
        }
    }

    /// <summary>Reports one read of a traced body of bytes.</summary>
    private void ReportBufferRead(int room, int read, bool endOfBody, bool isChunked)
    {
        if (tracing)
        {
            Events.ReportInfo(HttpClientReaderTraceLines.BufferRead(room, read, endOfBody));
            ReportClientRead(room, read, endOfBody, isChunked);
        }
    }

    /// <summary>
    /// Reports one read of a traced stream body: a <c>-T</c> upload's file reader line or a
    /// multipart body's two lines. A read that gives nothing ends an upload of unknown length;
    /// it fails a body of known length, and is not reported.
    /// </summary>
    private void ReportStreamRead(int room, int requested, long? length, int read, bool isChunked)
    {
        if (!tracing || (read == 0 && length is not null))
        {
            return;
        }

        long readSoFar = BytesWritten + read;
        bool endOfBody = length is { } total ? readSoFar == total : read == 0;
        string[] lines = IsUpload
            ? [HttpClientReaderTraceLines.InputRead(requested, length, readSoFar, read, endOfBody)]
            : HttpClientReaderTraceLines.MimeRead(requested, length.GetValueOrDefault(), readSoFar, read);
        foreach (string line in lines)
        {
            Events.ReportInfo(line);
        }

        ReportClientRead(room, read, endOfBody, isChunked);
    }

    /// <summary>
    /// Reports the client's line for one read; for a chunked body first the chunk encoder's lines,
    /// with the room and the bytes read counting the chunk framing and, at the body's end, the
    /// closing chunk, as curl 8.21.0 counts them (measured, BL-1214 Notes).
    /// </summary>
    private void ReportClientRead(int room, int read, bool endOfBody, bool isChunked)
    {
        int framed = read;
        if (isChunked)
        {
            framed = ReportChunkMade(read) + (endOfBody ? LastChunk.Length : 0);
            room += ChunkFramingReserve;
        }

        if (isChunked && endOfBody)
        {
            Events.ReportInfo(HttpClientReaderTraceLines.AddedLastChunk);
        }

        Events.ReportInfo(HttpClientReaderTraceLines.ClientRead(room, framed, endOfBody));
    }

    /// <summary>Reports the chunk a read made, if it read anything, and returns the chunk's length with its framing.</summary>
    private int ReportChunkMade(int read)
    {
        if (read == 0)
        {
            return 0;
        }

        Events.ReportInfo(HttpClientReaderTraceLines.MadeChunk(read));
        return ChunkSizeLine(read).Length + read + 2;
    }

    /// <summary>
    /// Returns how many bytes the next read may take: what the upload buffer has left once the
    /// head still sharing it and, for a chunk, <see cref="ChunkFramingReserve" /> are taken
    /// out. Only the first read shares the buffer with the head.
    /// </summary>
    private int NextReadRoom(bool isChunked)
    {
        int reserve = isChunked ? ChunkFramingReserve : 0;
        int room = UploadBufferSize - (headSent ? 0 : SharedHeadLength) - reserve;
        headSent = true;
        return room > 0 ? room : UploadBufferSize - reserve;
    }

    /// <summary>
    /// Reads from the body stream, taking a failed read as the end of the stream, as curl
    /// does, except a <see cref="RequestBodyReadFailedException" />, which fails the transfer
    /// with exit 26 and its message, as curl fails a multipart part its encoder refuses
    /// (measured, BL-385 Notes).
    /// </summary>
    /// <exception cref="HttpTransferException">The read threw a <see cref="RequestBodyReadFailedException" />.</exception>
    private static async ValueTask<int> ReadAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        try
        {
            return await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestBodyReadFailedException failed)
        {
            throw new HttpTransferException(CurlExitCode.ReadError, failed.Message);
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
            string message = IsUpload
                ? HttpTransferMessages.UploadEndedEarly(BytesWritten, needed)
                : HttpTransferMessages.BodyStreamEndedEarly(BytesWritten, needed);
            throw new HttpTransferException(CurlExitCode.ReadError, message);
        }
    }

    private async ValueTask WritePieceAsync(ReadOnlyMemory<byte> piece, bool isChunked, CancellationToken cancellationToken)
    {
        if (piece.IsEmpty)
        {
            return;
        }

        ReadOnlyMemory<byte> framed = Framed(piece, isChunked);
        if (EarlyResponseWatch is not { } watch)
        {
            await WriteAfterHeldHeadAsync(piece, framed, isChunked, cancellationToken).ConfigureAwait(false);
        }
        else if (!await watch.SendUnlessStoppedAsync(framed, cancellationToken).ConfigureAwait(false))
        {
            CutShort = true;
            unsent = piece.ToArray();
            return;
        }

        ReportSent(framed.Span);
        BytesWritten += piece.Length;
        Progress.ReportUploaded(BytesWritten, expectedLength);
    }

    /// <summary>
    /// Writes a piece after <see cref="HeldHead" />. On an HTTP/1.x connection a head still held
    /// goes out in one write with the start of the framed piece, as many of its bytes as fill
    /// <see cref="UploadBufferSize" />, as curl 8.21.0 sends its head and first body bytes from one
    /// upload buffer: a server that answers after its first read and closes would otherwise reset a
    /// connection whose body came after it (measured, BL-1215 Notes). An HTTP/2 or HTTP/3 stream
    /// takes its first write as the head, so there the head is written on its own.
    /// </summary>
    private async ValueTask WriteAfterHeldHeadAsync(ReadOnlyMemory<byte> piece, ReadOnlyMemory<byte> framed, bool isChunked, CancellationToken cancellationToken)
    {
        if (heldHead.IsEmpty || connection is IHttpStreamConnection)
        {
            await WriteHeldHeadAsync(cancellationToken).ConfigureAwait(false);
            await WriteFramedAsync(piece, isChunked, cancellationToken).ConfigureAwait(false);
            return;
        }

        ReadOnlyMemory<byte> head = heldHead;
        heldHead = ReadOnlyMemory<byte>.Empty;
        int shared = Math.Min(framed.Length, Math.Max(UploadBufferSize - head.Length, 0));
        await HttpConnectionSend.WriteAsync(connection, (byte[])[.. head.Span, .. framed.Span[..shared]], cancellationToken).ConfigureAwait(false);
        Events.ReportRequestHeader(head.Span);
        if (shared < framed.Length)
        {
            await HttpConnectionSend.WriteAsync(connection, framed[shared..], cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Writes a piece, for a chunked body with its chunk's size line and closing CRLF as
    /// writes of their own.
    /// </summary>
    private async ValueTask WriteFramedAsync(ReadOnlyMemory<byte> piece, bool isChunked, CancellationToken cancellationToken)
    {
        if (isChunked)
        {
            await HttpConnectionSend.WriteAsync(connection, ChunkSizeLine(piece.Length), cancellationToken).ConfigureAwait(false);
        }

        await HttpConnectionSend.WriteAsync(connection, piece, cancellationToken).ConfigureAwait(false);
        if (isChunked)
        {
            await HttpConnectionSend.WriteAsync(connection, "\r\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Counts <paramref name="bytes" /> as sent and reports them as one data event.
    /// </summary>
    private void ReportSent(ReadOnlySpan<byte> bytes)
    {
        BytesSent += bytes.Length;
        Events.ReportDataSent(bytes);
    }

    /// <summary>
    /// Gives a piece as one write: itself, or for a chunked body the whole chunk, size line
    /// and closing CRLF included.
    /// </summary>
    private static ReadOnlyMemory<byte> Framed(ReadOnlyMemory<byte> piece, bool isChunked) =>
        isChunked ? (byte[])[.. ChunkSizeLine(piece.Length), .. piece.Span, .. "\r\n"u8] : piece;

    private static byte[] ChunkSizeLine(int length) =>
        Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{length:x}\r\n"));
}
