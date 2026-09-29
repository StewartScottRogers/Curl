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
/// head is exit 16, and part way through the body exit 18. Failed reads and writes of the
/// connection itself pass through as <see cref="IOException" />.
/// </remarks>
/// <param name="session">The connection's HTTP/2 session.</param>
/// <param name="scheme">The URL's scheme, sent as <c>:scheme</c>.</param>
/// <param name="bodyLength">The request body's length, 0 when there is none, or <see langword="null" /> when unknown.</param>
internal sealed class Http2StreamConnection(Http2Session session, string scheme, long? bodyLength) : IHttpStreamConnection
{
    private readonly Queue<ReadOnlyMemory<byte>> received = new();

    private readonly MemoryStream trailers = new();

    private ReadOnlyMemory<byte> unread;

    private int streamId;

    private long bodyBytesSent;

    private bool isRequestEnded;

    private bool isResponseEnded;

    private bool isFinalHeadReceived;

    /// <inheritdoc />
    public bool IsSecure => session.Connection.IsSecure;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => session.Connection.RemoteEndPoint;

    /// <summary>
    /// Gets the response's trailers as <see cref="Http2ResponseHead.FormatTrailers" /> gives
    /// them, empty until the trailing header block has been read.
    /// </summary>
    public ReadOnlyMemory<byte> TrailerBytes => trailers.ToArray();

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
    /// Ends the request with an empty DATA frame carrying END_STREAM, unless it has ended
    /// already - it had no body, or the last body write ended it - or the response has ended,
    /// after which nothing more is sent.
    /// </summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the frame is written.</returns>
    public async ValueTask EndRequestAsync(CancellationToken cancellationToken)
    {
        if (isRequestEnded || isResponseEnded)
        {
            return;
        }

        _ = await session.Frames.WriteDataAsync(streamId, ReadOnlyMemory<byte>.Empty, isEndStream: true, cancellationToken).ConfigureAwait(false);
        isRequestEnded = true;
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
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the preface is written.</returns>
    public async ValueTask StartUpgradedAsync(CancellationToken cancellationToken)
    {
        streamId = await session.StartUpgradedStreamAsync(cancellationToken).ConfigureAwait(false);
        isRequestEnded = true;
    }

    /// <summary>
    /// Does nothing: the stream's connection is disposed by the handler that opened it.
    /// </summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async ValueTask StartAsync(ReadOnlyMemory<byte> head, CancellationToken cancellationToken)
    {
        isRequestEnded = bodyLength == 0;
        try
        {
            streamId = await session.StartStreamAsync(Http2RequestHeaders.Of(head.Span, scheme), isRequestEnded, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            throw new HttpTransferException(CurlExitCode.Http2, HttpTransferMessages.Http2FramingError);
        }
    }

    private async ValueTask SendDataAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        while (!data.IsEmpty && !isResponseEnded)
        {
            bool isLast = bodyBytesSent + data.Length == bodyLength;
            int sent = await session.Frames.WriteDataAsync(streamId, data, isLast, cancellationToken).ConfigureAwait(false);
            bodyBytesSent += sent;
            data = data[sent..];
            isRequestEnded = isLast && data.IsEmpty;
            if (sent == 0)
            {
                await ReceiveFrameAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask ReceiveFrameAsync(CancellationToken cancellationToken)
    {
        Http2StreamFrame? frame = await ReadFrameAsync(cancellationToken).ConfigureAwait(false);
        if (frame is null)
        {
            ThrowIfClosed();
        }
        else if (frame.Type == Http2FrameType.Headers)
        {
            await ReceiveHeadersAsync(frame, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // The frame layer returns DATA only for a stream still open, and this is the only one.
            await ReceiveDataAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<Http2StreamFrame?> ReadFrameAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await session.Frames.ReadFrameAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Http2StreamResetException reset)
        {
            throw new HttpTransferException(CurlExitCode.Http2Stream, HttpTransferMessages.Http2StreamNotClosedCleanly(reset.StreamId, reset.ErrorCode));
        }
        catch (Http2ProtocolException error)
        {
            throw new HttpTransferException(CurlExitCode.Http2, HttpTransferMessages.Http2ShutsDownConnection(error.ErrorCode));
        }
        catch (Http2GoAwayException)
        {
            throw new HttpTransferException(CurlExitCode.RecvError, HttpTransferMessages.ReceiveFailed);
        }
        catch (EndOfStreamException)
        {
            throw Closed();
        }
    }

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
    /// Takes a header block: decoded on every stream, as HPACK requires; on this stream a
    /// response head until the final one, then trailers.
    /// </summary>
    private async ValueTask ReceiveHeadersAsync(Http2StreamFrame frame, CancellationToken cancellationToken)
    {
        IReadOnlyList<HeaderField> fields = Decode(frame.Content);
        if (frame.StreamId != streamId)
        {
            return;
        }

        if (isFinalHeadReceived)
        {
            trailers.Write(Http2ResponseHead.FormatTrailers(fields));
        }
        else
        {
            int statusCode = Http2ResponseHead.StatusOf(fields) ?? throw await ResetMalformedAsync(cancellationToken).ConfigureAwait(false);
            received.Enqueue(Http2ResponseHead.Format(session.VersionName, statusCode, fields));
            isFinalHeadReceived = statusCode >= 200;
        }

        isResponseEnded = frame.IsEndStream;
    }

    private async ValueTask ReceiveDataAsync(Http2StreamFrame frame, CancellationToken cancellationToken)
    {
        if (!isFinalHeadReceived)
        {
            throw await ResetMalformedAsync(cancellationToken).ConfigureAwait(false);
        }

        received.Enqueue(frame.Content);
        isResponseEnded = frame.IsEndStream;
    }

    private IReadOnlyList<HeaderField> Decode(ReadOnlyMemory<byte> block)
    {
        try
        {
            return session.Decoder.Decode(block.Span);
        }
        catch (HpackDecodingException)
        {
            throw new HttpTransferException(CurlExitCode.Http2, HttpTransferMessages.Http2ShutsDownConnection(Http2ErrorCode.CompressionError));
        }
    }

    /// <summary>
    /// Resets the stream with PROTOCOL_ERROR for a malformed response - a head with no valid
    /// <c>:status</c>, or DATA before the head - as nghttp2 does, and gives the exit 92 failure
    /// to throw.
    /// </summary>
    private async ValueTask<HttpTransferException> ResetMalformedAsync(CancellationToken cancellationToken)
    {
        await session.Frames.ResetStreamAsync(streamId, Http2ErrorCode.ProtocolError, cancellationToken).ConfigureAwait(false);
        return new HttpTransferException(CurlExitCode.Http2Stream, HttpTransferMessages.Http2StreamNotClosedCleanly(streamId, Http2ErrorCode.ProtocolError));
    }
}
