using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

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
    /// The upload stream's trailer source when the body is a stream that implements
    /// <see cref="IUploadTrailerSource" />; <see langword="null" /> for any other body.
    /// </summary>
    private IUploadTrailerSource? trailerSource;

    /// <summary>
    /// The closing chunk with its trailers, made once per send by <see cref="LastChunkBytes" />;
    /// <see langword="null" /> until then.
    /// </summary>
    private byte[]? lastChunk;

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
    /// Gets a value indicating whether the body is a non-multipart stream (a <c>CURLOPT_READFUNCTION</c> POST), whose
    /// short read curl reports as <c>client read function EOF fail</c> like a <c>-T</c> upload (CGR-261).
    /// </summary>
    internal bool IsReadFunctionBody { get; init; }

    /// <summary>
    /// Gets where the body bytes sent so far (<see cref="BytesSent" />, chunk framing included) are
    /// reported, with the body's length when it is known: after each piece and the closing chunk, and
    /// once before the first byte unless the head waited for <c>100 Continue</c>, after which curl
    /// 8.21.0 draws no 0-byte line (measured, BL-2131 Notes).
    /// </summary>
    internal HttpTransferProgress Progress { get; init; } = HttpTransferProgress.Silent;

    private long? expectedLength;

    private long? startPosition;

    private ReadOnlyMemory<byte> unsent;

    /// <summary>
    /// The bytes of a chunked stream body of unknown length (<c>-T -</c>) held back until the next
    /// read tells whether the stream has ended, so the last chunk of data and the closing chunk go
    /// out in one write, as curl 8.21.0 fills its upload buffer before it sends: a server that
    /// answers on the data and closes would otherwise reset the connection before the closing
    /// chunk reaches it (BL-2341, ADR-0525); <see langword="null" /> for any other body.
    /// </summary>
    private List<byte>? heldBack;

    /// <summary>
    /// Gets a value indicating whether a chunked stream body of unknown length (<c>-T -</c>) sent
    /// after the wait for <c>100 Continue</c> holds each piece back until the next read, so the last
    /// piece and the closing chunk go out in one <see cref="HttpContinueWaitConnection.SendUnlessStoppedAsync" />,
    /// as <see cref="heldBack" /> does for a body sent without waiting (upstream test60 on macOS, BL-2371).
    /// </summary>
    private bool holdsWatchedPieces;

    /// <summary>
    /// The piece <see cref="holdsWatchedPieces" /> holds back, unframed; empty when none is held.
    /// </summary>
    private ReadOnlyMemory<byte> watchedHeldPiece;

    /// <summary>
    /// Gets the connection that waited for <c>100 Continue</c> and so watches for a
    /// status of 300 or above while the body is sent, or <see langword="null" /> when the
    /// request waited for nothing and the body is sent whole.
    /// </summary>
    internal HttpContinueWaitConnection? EarlyResponseWatch { get; init; }

    /// <summary>
    /// Gets the connection that reads the response's status line while a read of a stream body
    /// stays pending, as a paused libcurl read callback keeps it, or <see langword="null" /> when
    /// every read is simply awaited (upstream case 1533, BL-2729).
    /// </summary>
    internal HttpEarlyResponseConnection? EarlyResponseRead { get; init; }

    /// <summary>
    /// Gets a value indicating whether a read left pending by a status of 300 or above is kept and
    /// the rest of the body sent once the response is read (<see cref="WriteRestAfterResponseAsync" />),
    /// as libcurl's <c>CURLOPT_KEEP_SENDING_ON_ERROR</c> asks, rather than the body cut short.
    /// </summary>
    internal bool KeepsSendingOnError { get; init; }

    /// <summary>
    /// Gets the most bytes a second a body of bytes is sent at (<c>--limit-rate</c>), or
    /// <see langword="null" /> to send it unpaced; a stream body is never paced here (BL-2957).
    /// </summary>
    internal long? SendBytesPerSecond { get; init; }

    /// <summary>Gets the clock <see cref="SendBytesPerSecond" /> paces the body on.</summary>
    internal TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    /// <summary>What a read gives when it was left pending because a status of 300 or above came first.</summary>
    private const int ReadLeftPending = -1;

    /// <summary>The read of a stream body left pending for <see cref="WriteRestAfterResponseAsync" />; <see langword="null" /> when none is.</summary>
    private Task<int>? pendingRead;

    /// <summary>Whether a pending read is raced against the response; cleared once the rest of the body is sent after it.</summary>
    private bool racesPendingReads = true;

    /// <summary>The stream body being sent, kept for <see cref="WriteRestAfterResponseAsync" />.</summary>
    private StreamBody? sendingStream;

    /// <summary>Whether the stream body being sent is chunked, kept for <see cref="WriteRestAfterResponseAsync" />.</summary>
    private bool sendingChunked;

    /// <summary>The upload buffer a stream body is read into, kept so a read left pending still fills it.</summary>
    private byte[]? readBuffer;

    /// <summary>
    /// Gets a value indicating whether sending stopped before the end of the body because a
    /// status of 300 or above arrived first.
    /// </summary>
    internal bool CutShort { get; private set; }

    /// <summary>
    /// Gets the message an <see cref="IUploadRewinder" /> gave when <see cref="Rewound" /> failed
    /// to take the body back to its start, which fails the resend with exit 65 before it connects;
    /// <see langword="null" /> when no such rewind failed.
    /// </summary>
    internal string? RewindFailure { get; private set; }

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
    /// Whether the last read of a stream body did not complete at once, so the writer waited on its
    /// upload source as curl waits with the socket polled for reading (<see cref="ReceiveFailureOrAsync" />).
    /// </summary>
    private bool waitedOnSource;

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
    /// a read threw <see cref="RequestBodyReadFailedException" /> (its exit code), or the connection
    /// failed a write (exit 55).
    /// </exception>
    internal async ValueTask WriteAsync(HttpRequestBody body, bool isChunked, CancellationToken cancellationToken)
    {
        StartSending(body);
        if (EarlyResponseWatch is null)
        {
            Progress.ReportUploaded(BytesSent, expectedLength);
        }
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
        if (pendingRead is null)
        {
            await FinishWriteAsync(isChunked, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sends the rest of a stream body whose read was left pending when a status of 300 or above
    /// arrived first under <see cref="KeepsSendingOnError" />: waits for that read, then reads and
    /// sends to the end of the body without racing the response again, as libcurl 8.x sends the
    /// rest once the upload is unpaused (upstream case 1533, BL-2729). Does nothing when no read was
    /// left pending.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>A task that completes when the rest of the body has been written.</returns>
    /// <exception cref="HttpTransferException">As <see cref="WriteAsync" /> throws it.</exception>
    internal async ValueTask WriteRestAfterResponseAsync(CancellationToken cancellationToken)
    {
        if (pendingRead is null)
        {
            return;
        }

        racesPendingReads = false;
        await SendStreamReadsAsync(sendingStream!, sendingChunked, cancellationToken).ConfigureAwait(false);
        await FinishWriteAsync(sendingChunked, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the closing chunk of a chunked body not cut short, what is held back, flushes, and
    /// ends an HTTP/2 or HTTP/3 stream.
    /// </summary>
    private async ValueTask FinishWriteAsync(bool isChunked, CancellationToken cancellationToken)
    {
        if (isChunked && !CutShort)
        {
            await WriteLastChunkAsync(cancellationToken).ConfigureAwait(false);
        }

        await ReleaseHeldBackAsync(cancellationToken).ConfigureAwait(false);
        heldBack = null;
        await HttpConnectionSend.FlushAsync(connection, cancellationToken).ConfigureAwait(false);
        await HttpConnectionSend.EndStreamRequestAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resets what one send of <paramref name="body" /> keeps: its length when known, nothing held
    /// back, and its stream's <see cref="IUploadTrailerSource" /> with no closing chunk made yet.
    /// </summary>
    private void StartSending(HttpRequestBody body)
    {
        StreamBody? stream = body as StreamBody;
        expectedLength = stream is null ? ((BytesBody)body).Content.Length : stream.Length;
        heldBack = null;
        trailerSource = stream?.Content as IUploadTrailerSource;
        lastChunk = null;
        sendingStream = stream;
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
    /// rewound to where this writer began reading it; a stream that cannot seek but implements
    /// <see cref="IUploadRewinder" /> is rewound through it once some of it was read, and a failed
    /// rewind is kept in <see cref="RewindFailure" />; any other stream that cannot seek goes on from
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

        if (stream.Content is IUploadRewinder { HasBeenRead: true } rewinder)
        {
            RewindFailure = rewinder.RewindToStart();
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
        if (SendBytesPerSecond is { } rate)
        {
            await WriteBytesAtRateAsync(content, rate, isChunked, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (EarlyResponseWatch is null && !tracing && !isChunked)
        {
            await WritePieceAsync(content, isChunked, cancellationToken).ConfigureAwait(false);
            return;
        }

        await WriteBytesInPiecesAsync(content, isChunked, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a body of bytes in the pieces curl's upload buffer holds, reporting each read,
    /// until they are all sent or one is cut short.
    /// </summary>
    private async ValueTask WriteBytesInPiecesAsync(ReadOnlyMemory<byte> content, bool isChunked, CancellationToken cancellationToken)
    {
        while (BytesWritten < content.Length && !CutShort)
        {
            int room = NextReadRoom(isChunked);
            int length = (int)Math.Min(room, content.Length - BytesWritten);
            ReportBufferRead(room, length, BytesWritten + length == content.Length, isChunked);
            await WritePieceAsync(content.Slice((int)BytesWritten, length), isChunked, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Writes a body of bytes no faster than <paramref name="rate" /> bytes a second: pieces of at
    /// most <paramref name="rate" /> bytes, each after the wait on <see cref="TimeProvider" /> that
    /// keeps the bytes sent before it within the rate, as curl's <c>CURLOPT_MAX_SEND_SPEED_LARGE</c>
    /// pauses the send (BL-2957).
    /// </summary>
    private async ValueTask WriteBytesAtRateAsync(ReadOnlyMemory<byte> content, long rate, bool isChunked, CancellationToken cancellationToken)
    {
        long start = TimeProvider.GetTimestamp();
        while (BytesWritten < content.Length && !CutShort)
        {
            TimeSpan due = TimeSpan.FromSeconds((double)BytesWritten / rate) - TimeProvider.GetElapsedTime(start);
            if (due > TimeSpan.Zero)
            {
                await Task.Delay(due, TimeProvider, cancellationToken).ConfigureAwait(false);
            }

            int room = (int)Math.Min(NextReadRoom(isChunked), rate);
            int length = (int)Math.Min(room, content.Length - BytesWritten);
            ReportBufferRead(room, length, BytesWritten + length == content.Length, isChunked);
            await WritePieceAsync(content.Slice((int)BytesWritten, length), isChunked, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask WriteStreamAsync(StreamBody body, bool isChunked, CancellationToken cancellationToken)
    {
        StartReading(body, isChunked);
        sendingChunked = isChunked;
        await SendStreamReadsAsync(body, isChunked, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the stream body and sends each read until its end, a cut short, or a read left
    /// pending (<see cref="ReadLeftPending" />).
    /// </summary>
    private async ValueTask SendStreamReadsAsync(StreamBody body, bool isChunked, CancellationToken cancellationToken)
    {
        byte[] buffer = readBuffer ??= new byte[UploadBufferSize];
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
            if (!await SendReadAsync(body.Length, buffer, room, requested, read, isChunked, cancellationToken).ConfigureAwait(false))
            {
                return;
            }
        }
    }

    /// <summary>
    /// Reports and sends one read of a stream body.
    /// </summary>
    /// <returns>
    /// <see langword="false" /> when the read was left pending (<see cref="ReadLeftPending" />) or
    /// ended the body, so reading stops; <see langword="true" /> when the next read follows.
    /// </returns>
    /// <exception cref="HttpTransferException">A body of known length ended early (exit 26).</exception>
    private async ValueTask<bool> SendReadAsync(long? length, byte[] buffer, int room, int requested, int read, bool isChunked, CancellationToken cancellationToken)
    {
        if (read == ReadLeftPending)
        {
            return false;
        }

        ReportStreamRead(room, requested, length, read, isChunked);
        if (read == 0)
        {
            ThrowIfShort(length);
            return false;
        }

        await WriteReadPieceAsync(buffer.AsMemory(0, read), isChunked, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Notes where a seekable stream body begins, for a resend, and starts <see cref="heldBack" />
    /// for a chunked body of unknown length sent without waiting for <c>100 Continue</c>, or
    /// <see cref="holdsWatchedPieces" /> for one sent after that wait.
    /// </summary>
    private void StartReading(StreamBody body, bool isChunked)
    {
        startPosition = body.Content.CanSeek ? body.Content.Position : null;
        bool holds = isChunked && body.Length is null;
        heldBack = holds && EarlyResponseWatch is null ? [] : null;
        holdsWatchedPieces = holds && EarlyResponseWatch is not null;
    }

    /// <summary>
    /// Writes what <see cref="heldBack" /> holds, now that a read has given more, then the piece
    /// that read gave.
    /// </summary>
    /// <exception cref="HttpTransferException">
    /// The connection failed the write (exit 55), or, after a read the writer had to wait for, the
    /// connection reports a failure of its own when read (exit 56, <see cref="ReceiveFailureOrAsync" />).
    /// </exception>
    private async ValueTask WriteReadPieceAsync(ReadOnlyMemory<byte> piece, bool isChunked, CancellationToken cancellationToken)
    {
        try
        {
            await ReleaseHeldBackAsync(cancellationToken).ConfigureAwait(false);
            await WritePieceAsync(piece, isChunked, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpTransferException failed) when (waitedOnSource && failed.ExitCode == CurlExitCode.SendError)
        {
            throw await ReceiveFailureOrAsync(failed, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads the connection after a write that failed once the writer had waited on its upload
    /// source, and gives exit 56 with <c>Recv failure: </c> and the read's words when the read fails
    /// too: while curl 8.22.0 waits for upload data it polls the socket for reading only, and reads
    /// before it writes, so a peer that answered and reset meanwhile is met by <c>recv</c>, not
    /// <c>send</c> (BL-2956, ADR-0040 keeps a full-speed failed write at exit 55). A read that gives
    /// bytes or the end of the stream keeps <paramref name="sendFailure" />.
    /// </summary>
    private async ValueTask<HttpTransferException> ReceiveFailureOrAsync(HttpTransferException sendFailure, CancellationToken cancellationToken)
    {
        try
        {
            await connection.ReadAsync(new byte[1], cancellationToken).ConfigureAwait(false);
            return sendFailure;
        }
        catch (IOException failure)
        {
            return HttpTransferException.OfReceiveFailure(failure);
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

        long readSoFar = BytesWritten + watchedHeldPiece.Length + read;
        bool endOfBody = EndsBody(length, readSoFar, read);
        foreach (string line in StreamReadLines(requested, length, readSoFar, read, endOfBody))
        {
            Events.ReportInfo(line);
        }

        ReportClientRead(room, read, endOfBody, isChunked);
    }

    /// <summary>
    /// Tells whether a read ends a stream body: one of known length once all of it is read, one
    /// of unknown length when the read gives nothing.
    /// </summary>
    private static bool EndsBody(long? length, long readSoFar, int read) =>
        length is { } total ? readSoFar == total : read == 0;

    /// <summary>Gives the reader's lines for one read of a stream body: a <c>-T</c> upload's file reader line, or a multipart body's two lines.</summary>
    private string[] StreamReadLines(int requested, long? length, long readSoFar, int read, bool endOfBody) =>
        IsUpload
            ? [HttpClientReaderTraceLines.InputRead(requested, length, readSoFar, read, endOfBody)]
            : HttpClientReaderTraceLines.MimeRead(requested, length.GetValueOrDefault(), readSoFar, read);

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
            framed = ReportChunkMade(read) + (endOfBody ? LastChunkBytes().Length : 0);
            room += ChunkFramingReserve;
        }

        if (isChunked && endOfBody)
        {
            Events.ReportInfo(SuppliesTrailers
                ? HttpClientReaderTraceLines.AddedLastChunkWithTrailers
                : HttpClientReaderTraceLines.AddedLastChunk);
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
    /// with its exit code (26 unless aborted) and its message, as curl fails a multipart part its encoder refuses
    /// (measured, BL-385 Notes); before failing, it writes what <see cref="heldBack" /> holds, so
    /// the chunks read before the failure still reach the server.
    /// </summary>
    /// <exception cref="HttpTransferException">The read threw a <see cref="RequestBodyReadFailedException" />.</exception>
    private async ValueTask<int> ReadAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        try
        {
            return await ReadOrLeavePendingAsync(stream, buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestBodyReadFailedException failed)
        {
            await ReleaseHeldBackAsync(cancellationToken).ConfigureAwait(false);
            await SendWatchedHeldPieceAsync(ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false);
            throw new HttpTransferException(failed.ExitCode, failed.Message);
        }
        catch (IOException)
        {
            return 0;
        }
    }

    /// <summary>
    /// Starts a read of the body stream, or takes back the read <see cref="pendingRead" /> left
    /// pending. Under <see cref="EarlyResponseRead" />, a read that does not complete at once is
    /// raced against the response's status line (<see cref="RaceResponseAsync" />).
    /// </summary>
    private ValueTask<int> ReadOrLeavePendingAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (pendingRead is { } pending)
        {
            pendingRead = null;
            return new ValueTask<int>(pending);
        }

        if (EarlyResponseRead is not { } watch || !racesPendingReads)
        {
            ValueTask<int> plainRead = stream.ReadAsync(buffer, cancellationToken);
            waitedOnSource = !plainRead.IsCompleted;
            return plainRead;
        }

        CancellationTokenSource reading = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ValueTask<int> read = stream.ReadAsync(buffer, reading.Token);
        waitedOnSource = !read.IsCompleted;
        if (read.IsCompleted)
        {
            reading.Dispose();
            return read;
        }

        return RaceResponseAsync(watch, read.AsTask(), reading, cancellationToken);
    }

    /// <summary>
    /// Sends the request head and what is held back, then waits for whichever comes first: the
    /// pending <paramref name="read" />, or the response's status line. A status of 300 or above
    /// first leaves the read pending and gives <see cref="ReadLeftPending" />: under
    /// <see cref="KeepsSendingOnError" /> the read is kept in <see cref="pendingRead" /> for
    /// <see cref="WriteRestAfterResponseAsync" />, and otherwise it is cancelled and the body
    /// <see cref="CutShort" /> with nothing unsent kept, as libcurl 8.x stops sending on an early
    /// error response (upstream case 1533, BL-2729). Any other status, or none, waits for the read.
    /// </summary>
    private async ValueTask<int> RaceResponseAsync(HttpEarlyResponseConnection watch, Task<int> read, CancellationTokenSource reading, CancellationToken cancellationToken)
    {
        await WriteHeldHeadAsync(cancellationToken).ConfigureAwait(false);
        await ReleaseHeldBackAsync(cancellationToken).ConfigureAwait(false);
        await HttpConnectionSend.FlushAsync(connection, cancellationToken).ConfigureAwait(false);
        Task<int?> statusCode = watch.ReadStatusCodeAsync(cancellationToken);
        await Task.WhenAny(read, statusCode).ConfigureAwait(false);
        if (read.IsCompleted || !StopsSending(statusCode))
        {
            using (reading)
            {
                return await read.ConfigureAwait(false);
            }
        }

        if (KeepsSendingOnError)
        {
            pendingRead = read;
            return ReadLeftPending;
        }

        CutShort = true;
        unsent = ReadOnlyMemory<byte>.Empty;
        await reading.CancelAsync().ConfigureAwait(false);
        reading.Dispose();
        return ReadLeftPending;
    }

    /// <summary>Tells whether the status line read has a status of 300 or above.</summary>
    private static bool StopsSending(Task<int?> statusCode) =>
        statusCode.IsCompletedSuccessfully && statusCode.Result >= 300;

    private void ThrowIfShort(long? length)
    {
        if (length is { } needed)
        {
            string message = IsUpload || IsReadFunctionBody
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

        if (holdsWatchedPieces)
        {
            await HoldWatchedPieceAsync(piece, cancellationToken).ConfigureAwait(false);
            return;
        }

        ReadOnlyMemory<byte> framed = Framed(piece, isChunked);
        if (EarlyResponseWatch is not { } watch)
        {
            await WriteAfterHeldHeadAsync(piece, framed, isChunked, cancellationToken).ConfigureAwait(false);
        }
        else if (!await SendUnlessStoppedAsync(watch, piece, framed, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        ReportPieceSent(piece, framed);
    }

    /// <summary>
    /// Counts a piece as written and reports it, framed, as sent.
    /// </summary>
    private void ReportPieceSent(ReadOnlyMemory<byte> piece, ReadOnlyMemory<byte> framed)
    {
        Events.ReportDataSent(framed.Span);
        CountPieceSent(piece, framed);
    }

    /// <summary>
    /// Counts a piece as written and its framed bytes as sent, and reports the upload's progress.
    /// </summary>
    private void CountPieceSent(ReadOnlyMemory<byte> piece, ReadOnlyMemory<byte> framed)
    {
        BytesSent += framed.Length;
        BytesWritten += piece.Length;
        Progress.ReportUploaded(BytesSent, expectedLength);
    }

    /// <summary>
    /// Sends <paramref name="bytes" />, which begin with <paramref name="piece" /> framed, through
    /// <paramref name="watch" />; when a status of 300 or above stops it, marks the body
    /// <see cref="CutShort" /> with <paramref name="piece" /> unsent.
    /// </summary>
    private async ValueTask<bool> SendUnlessStoppedAsync(HttpContinueWaitConnection watch, ReadOnlyMemory<byte> piece, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        if (await watch.SendUnlessStoppedAsync(bytes, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        CutShort = true;
        unsent = piece.ToArray();
        return false;
    }

    /// <summary>
    /// Holds <paramref name="piece" /> back under <see cref="holdsWatchedPieces" /> and sends the
    /// piece held before it, now that this read has given more; when a status of 300 or above
    /// stops that send, both pieces are left unsent. The piece is reported as a data event when it is
    /// held, where curl 8.21.0's <c>--trace</c> shows it, before the next read (measured, BL-1214 Notes),
    /// and counted as sent only when it is.
    /// </summary>
    private async ValueTask HoldWatchedPieceAsync(ReadOnlyMemory<byte> piece, CancellationToken cancellationToken)
    {
        ReadOnlyMemory<byte> previous = watchedHeldPiece;
        watchedHeldPiece = piece.ToArray();
        Events.ReportDataSent(Framed(piece, true).Span);
        if (!await SendWatchedHeldPieceAsync(previous, ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false))
        {
            unsent = (byte[])[.. previous.Span, .. piece.Span];
        }
    }

    /// <summary>
    /// Sends the piece <see cref="holdsWatchedPieces" /> holds back, chunk framed and followed by
    /// <paramref name="closing" /> in the same write, and counts the piece as sent.
    /// </summary>
    /// <returns><see langword="false" /> when a status of 300 or above stopped the send.</returns>
    private ValueTask<bool> SendWatchedHeldPieceAsync(ReadOnlyMemory<byte> closing, CancellationToken cancellationToken)
    {
        ReadOnlyMemory<byte> held = watchedHeldPiece;
        watchedHeldPiece = ReadOnlyMemory<byte>.Empty;
        return SendWatchedHeldPieceAsync(held, closing, cancellationToken);
    }

    /// <summary>
    /// Sends <paramref name="held" />, chunk framed and followed by <paramref name="closing" /> in
    /// the same write, and counts it as sent; sends nothing for an empty piece.
    /// </summary>
    /// <returns><see langword="false" /> when a status of 300 or above stopped the send.</returns>
    private async ValueTask<bool> SendWatchedHeldPieceAsync(ReadOnlyMemory<byte> held, ReadOnlyMemory<byte> closing, CancellationToken cancellationToken)
    {
        if (held.IsEmpty)
        {
            return true;
        }

        ReadOnlyMemory<byte> framed = Framed(held, true);
        if (!await SendUnlessStoppedAsync(EarlyResponseWatch!, held, (byte[])[.. framed.Span, .. closing.Span], cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        CountPieceSent(held, framed);
        return true;
    }

    /// <summary>
    /// Writes the closing chunk: in one write with the piece <see cref="holdsWatchedPieces" /> holds
    /// back, if any, unless a status of 300 or above stops it; otherwise on its own.
    /// </summary>
    private async ValueTask WriteLastChunkAsync(CancellationToken cancellationToken)
    {
        byte[] last = LastChunkBytes();
        if (watchedHeldPiece.IsEmpty)
        {
            await SendAsync(last, cancellationToken).ConfigureAwait(false);
        }
        else if (!await SendWatchedHeldPieceAsync(last, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        ReportSent(last);
        Progress.ReportUploaded(BytesSent, expectedLength);
    }

    /// <summary>Gets a value indicating whether the body's stream has a trailer source that is set.</summary>
    private bool SuppliesTrailers => trailerSource is { SuppliesTrailers: true };

    /// <summary>
    /// Gives the closing chunk, made once per send when the body's end is first reached: the plain
    /// <c>0\r\n\r\n</c>, or with a <see cref="trailerSource" /> its well-formed trailer lines between
    /// the size line and the final CRLF, as curl 8.22.0's <c>add_last_chunk</c> makes it.
    /// </summary>
    /// <exception cref="HttpTransferException">The trailer source aborted (exit 42).</exception>
    private byte[] LastChunkBytes() => lastChunk ??= SuppliesTrailers ? LastChunkWithTrailers(trailerSource!) : LastChunk;

    /// <summary>
    /// Asks <paramref name="source" /> for its trailers and gives the closing chunk that carries them,
    /// reporting <c>Malformatted trailing header, skipping trailer</c> for each line curl 8.22.0 skips.
    /// </summary>
    /// <exception cref="HttpTransferException">The trailer source aborted (exit 42).</exception>
    private byte[] LastChunkWithTrailers(IUploadTrailerSource source)
    {
        if (!source.TryTakeTrailers(out IReadOnlyList<string> lines))
        {
            throw new HttpTransferException(CurlExitCode.AbortedByCallback, IUploadTrailerSource.AbortedMessage);
        }

        StringBuilder chunk = new("0\r\n");
        foreach (string line in lines)
        {
            if (IsWellFormedTrailer(line))
            {
                chunk.Append(line).Append("\r\n");
            }
            else
            {
                Events.ReportInfo(HttpTransferMessages.MalformattedTrailer);
            }
        }

        return Encoding.UTF8.GetBytes(chunk.Append("\r\n").ToString());
    }

    /// <summary>
    /// Tells whether a trailer line is one curl 8.22.0 sends: its first colon is followed by a space.
    /// </summary>
    private static bool IsWellFormedTrailer(string line)
    {
        int colon = line.IndexOf(':', StringComparison.Ordinal);
        return colon >= 0 && colon + 1 < line.Length && line[colon + 1] == ' ';
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
        await SendAsync((byte[])[.. head.Span, .. framed.Span[..shared]], cancellationToken).ConfigureAwait(false);
        Events.ReportRequestHeader(head.Span);
        if (shared < framed.Length)
        {
            await SendAsync(framed[shared..], cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Writes <paramref name="bytes" /> to the connection, or adds them to <see cref="heldBack" />
    /// while a chunked body of unknown length is being held back.
    /// </summary>
    private async ValueTask SendAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        if (heldBack is not null)
        {
            heldBack.AddRange(bytes.Span);
            return;
        }

        await HttpConnectionSend.WriteAsync(connection, bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the bytes <see cref="heldBack" /> holds, if any, in one write, and goes on holding
    /// back from empty.
    /// </summary>
    private async ValueTask ReleaseHeldBackAsync(CancellationToken cancellationToken)
    {
        if (heldBack is not { Count: > 0 } held)
        {
            return;
        }

        byte[] bytes = [.. held];
        held.Clear();
        await HttpConnectionSend.WriteAsync(connection, bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a piece, for a chunked body with its chunk's size line and closing CRLF as
    /// writes of their own.
    /// </summary>
    private async ValueTask WriteFramedAsync(ReadOnlyMemory<byte> piece, bool isChunked, CancellationToken cancellationToken)
    {
        if (isChunked)
        {
            await SendAsync(ChunkSizeLine(piece.Length), cancellationToken).ConfigureAwait(false);
        }

        await SendAsync(piece, cancellationToken).ConfigureAwait(false);
        if (isChunked)
        {
            await SendAsync("\r\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);
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
