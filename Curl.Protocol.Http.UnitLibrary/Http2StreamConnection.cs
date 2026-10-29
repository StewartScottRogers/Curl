using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// One HTTP/2 stream presented as the connection an HTTP/1.1 exchange runs on, as curl
/// 8.21.0's HTTP/2 layer presents it to its HTTP/1 code (BL-658). The first write is the
/// request head <see cref="HttpRequestHeadFormatter" /> formats, sent as a HEADERS block
/// (<see cref="Http2RequestHeaders" />); later writes are the body, sent as DATA within the
/// flow-control windows, the last one ending the stream when the body's length is known.
/// Reads give each response head as <see cref="Http2ResponseHead" /> formats it, then the
/// body's DATA, then zero once the stream has ended; trailers are kept in
/// <see cref="TrailerBytes" />.
/// </summary>
/// <remarks>
/// Failures are the ones curl reports (measured, BL-658 Notes): a stream the peer resets,
/// or a response with no valid <c>:status</c> or with DATA before its head, which this client
/// resets with PROTOCOL_ERROR, is exit 92; a protocol error or an undecodable header block is
/// exit 16 and a GOAWAY with an error exit 56; a connection the peer closes before the final
/// head is exit 16, and part way through the body exit 18. A response whose length does not add
/// up (RFC 9113 section 8.1.1: DATA past or short of its <c>content-length</c>, a body on HEAD, 204
/// or 304, a 1xx head ending the stream, an invalid or second <c>content-length</c>) makes this
/// client send GOAWAY PROTOCOL_ERROR, and is exit 16 (ADR-0847). Failed reads and writes of the
/// connection itself pass through as <see cref="IOException" />.
/// </remarks>
/// <param name="session">The connection's HTTP/2 session.</param>
/// <param name="scheme">The URL's scheme, sent as <c>:scheme</c>.</param>
/// <param name="bodyLength">The request body's length, 0 when there is none, or <see langword="null" /> when unknown.</param>
/// <param name="openedLines">Reports curl's <c>-v</c> lines for the stream once its HEADERS are sent, or <see langword="null" /> for none.</param>
/// <param name="frameLog">Where the stream's frames are logged (BL-1073), or <see langword="null" /> for nowhere.</param>
/// <param name="frameTrace">Writes the session's <c>--trace-config http/2</c> lines while this stream is the latest opened (BL-1167), or <see langword="null" /> for none.</param>
internal sealed class Http2StreamConnection(Http2Session session, string scheme, long? bodyLength, HttpStreamOpenedLines? openedLines = null, HttpFrameLog? frameLog = null, Http2FrameTrace? frameTrace = null) : IHttpStreamConnection
{
    /// <summary>The length of an RST_STREAM frame's payload: its error code.</summary>
    private const int ResetPayloadLength = 4;

    private readonly Queue<ReadOnlyMemory<byte>> received = new();

    private readonly MemoryStream trailers = new();

    /// <summary>The frames and resets the session has handed this stream and it has not yet taken.</summary>
    private readonly ConcurrentQueue<ReceivedFrame> inbox = new();

    private ReadOnlyMemory<byte> unread;

    private int streamId;

    private long bodyBytesSent;

    private bool isRequestEnded;

    private bool isReceiveWindowGrown;

    private bool isResponseEnded;

    private bool isFinalHeadReceived;

    private bool isHeadRequest;

    /// <summary>The body length the final head promises (RFC 9113 section 8.1.1), or <see langword="null" /> when it promises none.</summary>
    private long? promisedBodyLength;

    private long bodyBytesReceived;

    /// <summary>The failure of a response this client shut the connection down for, thrown once the bytes before it are read.</summary>
    private HttpTransferException? malformedResponseFailure;

    /// <inheritdoc />
    public bool IsSecure => session.Connection.IsSecure;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => session.Connection.RemoteEndPoint;

    /// <summary>
    /// Gets the response's trailers as <see cref="Http2ResponseHead.FormatTrailers" /> gives
    /// them, empty until the trailing header block has been read.
    /// </summary>
    public ReadOnlyMemory<byte> TrailerBytes => trailers.ToArray();

    /// <summary>
    /// Gets a value indicating whether the session has handed this stream a frame or a reset
    /// it has not yet taken.
    /// </summary>
    internal bool HasReceived => !inbox.IsEmpty;

    /// <summary>Gets the stream's identifier, 0 until its head is written.</summary>
    internal int StreamId => streamId;

    /// <summary>Gets where the stream's frames are logged: its transfer's diagnostic log.</summary>
    internal HttpFrameLog FrameLog { get; } = frameLog ?? HttpFrameLog.Silent;

    /// <summary>Gets what writes the <c>--trace-config http/2</c> lines for this stream's transfer, or <see langword="null" /> for none.</summary>
    internal Http2FrameTrace? FrameTrace => frameTrace;

    /// <summary>Gets what reports this stream's transfer's <c>-v</c> lines, or <see langword="null" /> for none.</summary>
    internal HttpStreamOpenedLines? OpenedLines => openedLines;

    /// <summary>
    /// Gets or sets who is offered the pushes the server promises on this stream (BL-2516), or
    /// <see langword="null" /> when its transfer takes none, so each is refused.
    /// </summary>
    internal IHttp2PushSink? PushSink { get; set; }

    /// <summary>
    /// Gets or sets the rate (<c>--limit-rate</c>) the transfer receives at, or <see langword="null" /> for none: it
    /// shrinks the receive window the stream is grown to (CGR-281, <see cref="Http2Session.StreamReceiveWindowSizeFor" />).
    /// </summary>
    internal long? ReceiveBytesPerSecond { get; set; }

    /// <summary>
    /// Makes this the stream a server's PUSH_PROMISE reserved: it sends nothing, as the client's
    /// side of a promised stream is closed, and reads the pushed response from it.
    /// </summary>
    /// <param name="promisedStreamId">The promised stream's identifier.</param>
    internal void AdoptPromised(int promisedStreamId)
    {
        streamId = promisedStreamId;
        isRequestEnded = true;
        isReceiveWindowGrown = true;
    }

    /// <summary>
    /// Echoes a line of this stream's response head, just reported as a <c>&lt;</c> line, as a
    /// <c>--trace-config http/2</c> <c>status:</c> or <c>header:</c> line (BL-1205); nothing when the
    /// transfer traces nothing.
    /// </summary>
    /// <param name="line">The line as reported, its line end included.</param>
    internal void EchoResponseLine(byte[] line) => frameTrace?.ResponseLineReported(streamId, line);

    /// <summary>
    /// Reports curl's <c>OPENED stream</c> lines for this stream, once it has its identifier and
    /// before its HEADERS go out, as curl writes them before nghttp2 submits the frame.
    /// </summary>
    /// <param name="id">The stream's identifier.</param>
    /// <param name="fields">The header list the stream sends.</param>
    internal void ReportOpened(int id, IReadOnlyList<HeaderField> fields) => openedLines?.Report(session.VersionName, id, fields);

    /// <inheritdoc />
    /// <exception cref="HttpTransferException">The stream failed (exit 16, 18, 56 or 92).</exception>
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        while (unread.IsEmpty)
        {
            if (received.TryDequeue(out unread))
            {
                continue;
            }

            if (isResponseEnded)
            {
                return 0;
            }

            await ReceiveFrameAsync(cancellationToken).ConfigureAwait(false);
        }

        int count = Math.Min(unread.Length, buffer.Length);
        unread[..count].CopyTo(buffer);
        unread = unread[count..];
        return count;
    }

    /// <inheritdoc />
    /// <exception cref="HttpTransferException">The stream failed while DATA waited for window (exit 16, 18, 56 or 92).</exception>
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (streamId == 0)
        {
            await StartAsync(buffer, cancellationToken).ConfigureAwait(false);
            return;
        }

        await SendDataAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => session.Connection.FlushAsync(cancellationToken);

    /// <summary>
    /// Ends the request with an empty DATA frame carrying END_STREAM, then grows the stream's
    /// receive window, unless it has ended already - it had no body, or the last body write
    /// ended it - or the response has ended, after which nothing more is sent.
    /// </summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the frame is written.</returns>
    public async ValueTask EndRequestAsync(CancellationToken cancellationToken)
    {
        if (isRequestEnded || isResponseEnded)
        {
            return;
        }

        _ = await session.WriteDataAsync(streamId, ReadOnlyMemory<byte>.Empty, isEndStream: true, cancellationToken).ConfigureAwait(false);
        FrameLog.FrameSent("DATA", streamId, 0);
        isRequestEnded = true;
        await GrowReceiveWindowOnceAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the rest of the stream, dropping any DATA past the body, so trailers that follow
    /// a body framed by its Content-Length are read.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the stream has ended.</returns>
    /// <exception cref="HttpTransferException">The stream failed (exit 16, 18, 56 or 92).</exception>
    public async ValueTask ReadToEndAsync(CancellationToken cancellationToken)
    {
        while (!isResponseEnded)
        {
            await ReceiveFrameAsync(cancellationToken).ConfigureAwait(false);
        }

        received.Clear();
        unread = ReadOnlyMemory<byte>.Empty;
    }

    /// <summary>
    /// Makes this the stream an HTTP/1.1 request upgraded to h2c continues on: sends the client
    /// preface and takes stream 1, whose request went out as HTTP/1.1 and so has ended, and
    /// whose response is read from it (<see cref="Http2Session.StartUpgradedStreamAsync" />).
    /// Nothing may be written to it afterwards.
    /// </summary>
    /// <param name="isHead">Whether the upgraded request is a HEAD, whose response has no body (BL-3667).</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the preface is written.</returns>
    public async ValueTask StartUpgradedAsync(bool isHead, CancellationToken cancellationToken)
    {
        isHeadRequest = isHead;
        streamId = await session.StartUpgradedStreamAsync(this, cancellationToken).ConfigureAwait(false);
        isRequestEnded = true;
        isReceiveWindowGrown = true;
    }

    /// <summary>
    /// Gives up the response's body without reading it, as curl does with a body it ignores
    /// before a retry or a followed redirect: unless the stream has already ended, it is reset
    /// with STREAM_CLOSED, <c>000004 03 00 00000001 00000005</c> on stream 1 (measured, BL-970
    /// Notes), and the frames the peer still sends on it are dropped.
    /// </summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the reset is written, or at once when none is due.</returns>
    public async ValueTask AbandonResponseAsync(CancellationToken cancellationToken)
    {
        if (isResponseEnded)
        {
            return;
        }

        await ResetAsync(Http2ErrorCode.StreamClosed, cancellationToken).ConfigureAwait(false);
        isResponseEnded = true;
    }

    /// <summary>
    /// Does nothing: the stream's connection is disposed by the handler that opened it.
    /// </summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Hands the stream a frame the session read for it, with the header block's fields when it
    /// is HEADERS; the stream takes it on its next read.
    /// </summary>
    /// <param name="frame">The DATA frame or completed header block.</param>
    /// <param name="fields">The decoded header block, or <see langword="null" /> for DATA.</param>
    internal void Take(Http2StreamFrame frame, IReadOnlyList<HeaderField>? fields) => inbox.Enqueue(new ReceivedFrame(frame, fields, null));

    /// <summary>
    /// Hands the stream the peer's reset of it, read while another stream was reading; the
    /// stream's next read fails with it (exit 92).
    /// </summary>
    /// <param name="reset">The reset.</param>
    internal void TakeReset(Http2StreamResetException reset) => inbox.Enqueue(new ReceivedFrame(null, null, reset));

    private async ValueTask StartAsync(ReadOnlyMemory<byte> head, CancellationToken cancellationToken)
    {
        try
        {
            List<HeaderField> fields = Http2RequestHeaders.Of(head.Span, scheme);

            // An empty body the request declares (-d '' sends Content-Length: 0) still goes out as
            // an empty DATA frame ending the stream, as curl does (CGR-276); only a request that
            // declares none ends on its HEADERS.
            isRequestEnded = bodyLength == 0 && !fields.Exists(field => field.Name == "content-length");
            isHeadRequest = fields.Contains(new HeaderField(":method", "HEAD"));
            streamId = await session.StartStreamAsync(this, fields, isRequestEnded, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            throw new HttpTransferException(CurlExitCode.Http2, HttpTransferMessages.Http2FramingError);
        }
        catch (Exception exception) when (FailureOf(exception) is { } failure)
        {
            throw failure;
        }

        if (isRequestEnded)
        {
            await GrowReceiveWindowOnceAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask SendDataAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        while (!data.IsEmpty && !isResponseEnded)
        {
            bool isLast = bodyBytesSent + data.Length == bodyLength;
            int sent = await session.WriteDataAsync(streamId, data, isLast, cancellationToken).ConfigureAwait(false);
            bodyBytesSent += sent;
            data = data[sent..];
            isRequestEnded = isLast && data.IsEmpty;
            if (sent == 0)
            {
                await ReceiveFrameAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                FrameLog.FrameSent("DATA", streamId, sent);
            }
        }

        if (isRequestEnded)
        {
            await GrowReceiveWindowOnceAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sends curl's WINDOW_UPDATEs for this stream (<see cref="Http2Session.GrowStreamReceiveWindowAsync" />)
    /// the first time it is called: curl 8.18.0 sends them right after the frame that ends the
    /// request - HEADERS for a request with no body, else its last DATA (measured, BL-817
    /// Notes) - and this is also called before the first frame is read, for a body still
    /// waiting for window.
    /// </summary>
    private async ValueTask GrowReceiveWindowOnceAsync(CancellationToken cancellationToken)
    {
        if (isReceiveWindowGrown)
        {
            return;
        }

        isReceiveWindowGrown = true;
        await session.GrowStreamReceiveWindowAsync(streamId, ReceiveBytesPerSecond, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Takes the next frame the session handed this stream, reading one from the connection
    /// first when none is waiting; a frame read for another stream goes to that stream and
    /// leaves this one to call again.
    /// </summary>
    private async ValueTask ReceiveFrameAsync(CancellationToken cancellationToken)
    {
        if (malformedResponseFailure is not null)
        {
            throw malformedResponseFailure;
        }

        await GrowReceiveWindowOnceAsync(cancellationToken).ConfigureAwait(false);
        if (!inbox.TryDequeue(out ReceivedFrame? next))
        {
            await ReadFrameAsync(cancellationToken).ConfigureAwait(false);
            if (!inbox.TryDequeue(out next))
            {
                ThrowIfClosed();
                return;
            }
        }

        await TakeAsync(next, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask TakeAsync(ReceivedFrame next, CancellationToken cancellationToken)
    {
        if (next.Reset is { } reset)
        {
            EndOnCleanReset(reset);
        }
        else if (next.Frame!.Type == Http2FrameType.Headers)
        {
            await ReceiveHeadersAsync(next.Frame, next.Fields!, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await ReceiveDataAsync(next.Frame, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask ReadFrameAsync(CancellationToken cancellationToken)
    {
        try
        {
            await session.ReceiveAsync(this, cancellationToken).ConfigureAwait(false);
        }
        catch (Http2StreamResetException reset)
        {
            EndOnCleanReset(reset);
        }
        catch (Exception exception) when (FailureOf(exception) is { } failure)
        {
            throw failure;
        }
    }

    /// <summary>
    /// Ends the response at a RST_STREAM NO_ERROR that came after the final head, which curl
    /// with nghttp2 takes as the body's end (CGR-141, measured, BL-3670 Notes); throws the
    /// transfer failure for any other reset.
    /// </summary>
    private void EndOnCleanReset(Http2StreamResetException reset)
    {
        if (reset.ErrorCode != Http2ErrorCode.NoError || !isFinalHeadReceived)
        {
            throw FailureOf(reset)!;
        }

        isResponseEnded = true;
    }

    /// <summary>
    /// Gives the exit 92 message for a reset: NO_ERROR, which reaches here only before the final
    /// head, says the stream closed cleanly too early; any other code says it did not close cleanly.
    /// </summary>
    private static string ResetMessageOf(Http2StreamResetException reset) => reset.ErrorCode == Http2ErrorCode.NoError
        ? HttpTransferMessages.Http2StreamClosedBeforeHead(reset.StreamId)
        : HttpTransferMessages.Http2StreamNotClosedCleanly(reset.StreamId, reset.ErrorCode);

    /// <summary>
    /// Gives the transfer failure curl reports for a failure of the stream or its connection,
    /// or <see langword="null" /> for one that passes through, such as an <see cref="IOException" />.
    /// A reset with HTTP_1_1_REQUIRED is marked <see cref="HttpTransferException.RequiresHttp11" />,
    /// which the handler sends again over HTTP/1.1 on a new connection (CGR-032); any reset is
    /// marked <see cref="HttpTransferException.LeavesConnectionIntact" />, since it ends only the
    /// stream and the next transfer goes on the same connection (CGR-140).
    /// </summary>
    private HttpTransferException? FailureOf(Exception exception) => IsRefusal(exception) ? Refused(exception) : exception switch
    {
        Http2StreamResetException reset => new(CurlExitCode.Http2Stream, ResetMessageOf(reset))
        {
            RequiresHttp11 = reset.ErrorCode == Http2ErrorCode.Http11Required,
            LeavesConnectionIntact = true,
        },
        Http2ProtocolException error => ConnectionErrorOf(error),
        Http2GoAwayException => new(CurlExitCode.RecvError, HttpTransferMessages.Http2GoAwayReceiveFailure),
        EndOfStreamException => Closed(),
        _ => null,
    };

    /// <summary>
    /// Gives the transfer failure for a connection error: exit 92 for a frame nghttp2 calls
    /// invalid (a PUSH_PROMISE with push disabled, CGR-142, measured with curl 8.22.0, BL-3912
    /// Notes), else exit 16 saying nghttp2 shut the connection down.
    /// </summary>
    private static HttpTransferException ConnectionErrorOf(Http2ProtocolException error) => error.InvalidFrame is { } frame
        ? new(CurlExitCode.Http2Stream, HttpTransferMessages.Http2PushPromiseReceivedWithPushDisabled(frame.StreamId, frame.Payload.Length, frame.HasFlag(Http2FrameFlags.EndHeaders)))
        : new(CurlExitCode.Http2, IsCallbackFailure(error)
            ? HttpTransferMessages.Http2RecvCallbackFailed
            : HttpTransferMessages.Http2ShutsDownConnection(error.ErrorCode));

    /// <summary>
    /// Tells whether nghttp2 fails its receive callback for the error: a flow control violation or an invalid
    /// SETTINGS value (CGR-272); other protocol errors keep the connection-shutdown wording (BL-658).
    /// </summary>
    private static bool IsCallbackFailure(Http2ProtocolException error) =>
        error.ErrorCode == Http2ErrorCode.FlowControlError
        || (error.ErrorCode == Http2ErrorCode.ProtocolError && error.Message.Contains(": SETTINGS_", StringComparison.Ordinal));

    /// <summary>
    /// Tells whether the server refused this stream: reset it with REFUSED_STREAM, or sent a
    /// GOAWAY whose last stream ID is below it, which nghttp2 closes the stream for with
    /// REFUSED_STREAM too.
    /// </summary>
    private bool IsRefusal(Exception exception) =>
        exception is Http2StreamResetException { ErrorCode: Http2ErrorCode.RefusedStream }
        || (exception is Http2GoAwayException goAway && goAway.GoAway.LastStreamId < streamId);

    /// <summary>
    /// Gives the failure for a stream the server refused (<see cref="IsRefusal" />): exit 56 marked
    /// <see cref="HttpTransferException.IsStreamRefused" />, which the handler sends again on a
    /// new connection as curl 8.18.0 with nghttp2 does, with no <c>-v</c> line of its own
    /// (measured, BL-3560 Notes).
    /// </summary>
    private static HttpTransferException Refused(Exception exception) =>
        new(CurlExitCode.RecvError, exception is Http2GoAwayException ? HttpTransferMessages.Http2GoAwayReceiveFailure : HttpTransferMessages.ReceiveFailed) { IsStreamRefused = true };

    private void ThrowIfClosed()
    {
        if (session.Frames.IsClosedByPeer)
        {
            throw Closed();
        }
    }

    private HttpTransferException Closed() => isFinalHeadReceived
        ? new(CurlExitCode.PartialFile, HttpTransferMessages.PartialFile)
        : new(CurlExitCode.Http2, HttpTransferMessages.Http2FramingError);

    /// <summary>
    /// Takes a header block of this stream, decoded by the session: a response head until the
    /// final one, then trailers.
    /// </summary>
    private async ValueTask ReceiveHeadersAsync(Http2StreamFrame frame, IReadOnlyList<HeaderField> fields, CancellationToken cancellationToken)
    {
        int invalidIndex = Http2ResponseHead.IndexOfInvalidField(fields, isFinalHeadReceived);
        if (invalidIndex >= 0)
        {
            await RejectInvalidFieldAsync(fields, invalidIndex, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (isFinalHeadReceived && !frame.IsEndStream)
        {
            // Trailers must end the stream (RFC 9113 section 8.1); nghttp2 shuts the connection down (measured, BL-3664 Notes).
            await ShutDownMalformedAsync(CurlExitCode.Http2, HttpTransferMessages.Http2ShutsDownConnection(Http2ErrorCode.ProtocolError), cancellationToken).ConfigureAwait(false);
            return;
        }

        if (isFinalHeadReceived)
        {
            trailers.Write(Http2ResponseHead.FormatTrailers(fields));
        }
        else
        {
            int statusCode = Http2ResponseHead.StatusOf(fields) ?? throw await RejectHeadWithoutValidStatusAsync(frame, fields, cancellationToken).ConfigureAwait(false);
            if (!await IsHeadTakenAsync(statusCode, fields, frame.IsEndStream, cancellationToken).ConfigureAwait(false))
            {
                return;
            }
        }

        await EndResponseAsync(frame.IsEndStream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Fails the response at a field nghttp2 rejects (<see cref="Http2ResponseHead.IndexOfInvalidField" />,
    /// measured, BL-3664 Notes): in a head, curl has written the status line and the lines before
    /// the field but not the empty line; nghttp2 shuts the connection down, and curl exits 16, or
    /// 8 when a regular field came before any <c>:status</c>, which leaves its HTTP/1 parser a
    /// header line with no status line.
    /// </summary>
    private async ValueTask RejectInvalidFieldAsync(IReadOnlyList<HeaderField> fields, int invalidIndex, CancellationToken cancellationToken)
    {
        List<HeaderField> fieldsBefore = isFinalHeadReceived ? [] : fields.Take(invalidIndex).ToList();
        int? statusCode = Http2ResponseHead.StatusOf(fieldsBefore);
        if (statusCode is { } writtenStatusCode)
        {
            received.Enqueue(HeadWithoutEmptyLine(writtenStatusCode, fieldsBefore));
        }

        CurlExitCode exitCode = statusCode is null && fieldsBefore.Count > 0 ? CurlExitCode.WeirdServerReply : CurlExitCode.Http2;
        await ShutDownMalformedAsync(exitCode, InvalidHeaderFieldReceived(streamId, fields[invalidIndex]), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Takes a response head as nghttp2 checks it (measured, BL-3667 Notes): a 1xx head that
    /// ends the stream is a protocol error; curl has written the lines but not the empty line,
    /// and nghttp2 shuts the connection down.
    /// </summary>
    /// <returns><see langword="true" /> for a head taken, <see langword="false" /> for one the connection was shut down for.</returns>
    private async ValueTask<bool> IsHeadTakenAsync(int statusCode, IReadOnlyList<HeaderField> fields, bool isEndStream, CancellationToken cancellationToken)
    {
        if (MalformedHeadOf(statusCode, fields, isEndStream) is { } malformed)
        {
            received.Enqueue(malformed.Written);
            await ShutDownMalformedAsync(CurlExitCode.Http2, malformed.Message, cancellationToken).ConfigureAwait(false);
            return false;
        }

        received.Enqueue(Http2ResponseHead.Format(session.VersionName, statusCode, fields));
        isFinalHeadReceived = statusCode >= 200;
        promisedBodyLength = PromisedBodyLengthOf(statusCode, fields);
        return true;
    }

    /// <summary>
    /// Gives the body length a head promises: none for a response to HEAD, a 204 or a 304, as
    /// nghttp2 counts it, else its <c>content-length</c>, or <see langword="null" /> when it has none.
    /// </summary>
    private long? PromisedBodyLengthOf(int statusCode, IReadOnlyList<HeaderField> fields) =>
        isHeadRequest || statusCode is 204 or 304 ? 0 : ContentLengthOf(fields);

    /// <summary>
    /// Gives what curl writes of a head nghttp2 rejects and the message it fails with, or
    /// <see langword="null" /> for a head it takes (<see cref="IsHeadTakenAsync" />).
    /// </summary>
    private (byte[] Written, string Message)? MalformedHeadOf(int statusCode, IReadOnlyList<HeaderField> fields, bool isEndStream) =>
        statusCode < 200 && isEndStream
            ? (HeadWithoutEmptyLine(statusCode, fields), HttpTransferMessages.Http2ShutsDownConnection(Http2ErrorCode.ProtocolError))
            : null;

    private async ValueTask ReceiveDataAsync(Http2StreamFrame frame, CancellationToken cancellationToken)
    {
        if (!isFinalHeadReceived)
        {
            throw await ResetMalformedAsync(cancellationToken).ConfigureAwait(false);
        }

        bodyBytesReceived += frame.Content.Length;
        if (bodyBytesReceived > promisedBodyLength)
        {
            await ShutDownMalformedAsync(CurlExitCode.Http2, HttpTransferMessages.Http2ShutsDownConnection(Http2ErrorCode.ProtocolError), cancellationToken).ConfigureAwait(false);
            return;
        }

        received.Enqueue(frame.Content);
        await EndResponseAsync(frame.IsEndStream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Ends the response when its last frame came, unless its body is shorter than its
    /// <c>content-length</c>, which nghttp2 shuts the connection down for (RFC 9113 section 8.1.1).
    /// </summary>
    private async ValueTask EndResponseAsync(bool isEndStream, CancellationToken cancellationToken)
    {
        if (isEndStream && isFinalHeadReceived && bodyBytesReceived != (promisedBodyLength ?? bodyBytesReceived))
        {
            await ShutDownMalformedAsync(CurlExitCode.Http2, HttpTransferMessages.Http2ShutsDownConnection(Http2ErrorCode.ProtocolError), cancellationToken).ConfigureAwait(false);
            return;
        }

        isResponseEnded = isEndStream;
    }

    /// <summary>
    /// Shuts the connection down for a malformed response as nghttp2 does: GOAWAY with
    /// PROTOCOL_ERROR, last stream 0 and no debug data (measured, BL-3667 Notes). The failure,
    /// <paramref name="exitCode" /> with <paramref name="message" />, is thrown by the stream's
    /// next read, after the bytes curl wrote before it.
    /// </summary>
    private async ValueTask ShutDownMalformedAsync(CurlExitCode exitCode, string message, CancellationToken cancellationToken)
    {
        malformedResponseFailure = new HttpTransferException(exitCode, message);
        await session.Frames.SendGoAwayAsync(Http2ErrorCode.ProtocolError, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gives the head's first <c>content-length</c> that nghttp2 does not ignore, or
    /// <see langword="null" /> when it has none. A field whose value holds a CR, LF or NUL is
    /// skipped (<see cref="Http2ResponseHead.IsIgnored" />), as <see cref="Http2ResponseHead.IndexOfInvalidField" />
    /// skips it; every other one is already checked by that method.
    /// </summary>
    private static long? ContentLengthOf(IReadOnlyList<HeaderField> fields) =>
        fields.Where(field => field.Name == "content-length" && !Http2ResponseHead.IsIgnored(field)).Select(field => (long?)long.Parse(field.Value, NumberStyles.None, CultureInfo.InvariantCulture)).FirstOrDefault();

    /// <summary>Gives the status line and header lines curl writes for a head it fails part way through: no empty line.</summary>
    private byte[] HeadWithoutEmptyLine(int statusCode, IReadOnlyList<HeaderField> fields) =>
        Http2ResponseHead.Format(session.VersionName, statusCode, fields)[..^2];

    /// <summary>The exit 16 message curl gives for a header field nghttp2 rejects (measured, BL-3667 Notes).</summary>
    private static string InvalidHeaderFieldReceived(int streamId, HeaderField field) =>
        string.Create(CultureInfo.InvariantCulture, $"Invalid HTTP header field was received: frame type: 1, stream: {streamId}, name: [{field.Name}], value: [{field.Value}]");

    /// <summary>
    /// Resets the stream with PROTOCOL_ERROR for a malformed response - DATA before the head -
    /// as nghttp2 does, and gives the exit 92 failure to throw.
    /// </summary>
    private async ValueTask<HttpTransferException> ResetMalformedAsync(CancellationToken cancellationToken)
    {
        await ResetAsync(Http2ErrorCode.ProtocolError, cancellationToken).ConfigureAwait(false);
        return new HttpTransferException(CurlExitCode.Http2Stream, HttpTransferMessages.Http2StreamNotClosedCleanly(streamId, Http2ErrorCode.ProtocolError));
    }

    /// <summary>
    /// Resets the stream with PROTOCOL_ERROR for a response head whose <c>:status</c> is not
    /// three digits, as nghttp2 does, and gives the failure curl 8.22.0 reports (CGR-142,
    /// measured, BL-3912 Notes): exit 92 naming the invalid field when the head has a
    /// <c>:status</c>, exit 8 naming the invalid HEADERS frame when it has none.
    /// </summary>
    private async ValueTask<HttpTransferException> RejectHeadWithoutValidStatusAsync(Http2StreamFrame frame, IReadOnlyList<HeaderField> fields, CancellationToken cancellationToken)
    {
        await ResetAsync(Http2ErrorCode.ProtocolError, cancellationToken).ConfigureAwait(false);
        HeaderField status = fields.FirstOrDefault(field => field.Name == ":status");
        return status.Name is null
            ? new HttpTransferException(CurlExitCode.WeirdServerReply, HttpTransferMessages.Http2HeadWithoutStatusReceived(streamId, frame.Content.Length, frame.IsEndStream))
            : new HttpTransferException(CurlExitCode.Http2Stream, InvalidHeaderFieldReceived(streamId, status));
    }

    /// <summary>Resets the stream with <paramref name="errorCode" /> and logs the RST_STREAM sent.</summary>
    private async ValueTask ResetAsync(Http2ErrorCode errorCode, CancellationToken cancellationToken)
    {
        await session.ResetStreamAsync(streamId, errorCode, cancellationToken).ConfigureAwait(false);
        FrameLog.FrameSent("RST_STREAM", streamId, ResetPayloadLength);
    }
}
