using System.Net;
using Curl.Http2;
using Curl.Http3;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// One HTTP/3 request stream presented as the connection an HTTP/1.1 exchange runs on, as
/// curl's HTTP/3 layer presents it to its HTTP/1 code (BL-731, ADR-0172). The first write is
/// the request head <see cref="HttpRequestHeadFormatter" /> formats, sent on a new
/// bidirectional QUIC stream as one <c>HEADERS</c> frame (<see cref="Http2RequestHeaders" />,
/// encoded with QPACK); later writes are the body, sent as <c>DATA</c> frames, the last one
/// ending the stream when the body's length is known. Reads give each response head as
/// <see cref="Http2ResponseHead" /> formats it, <c>HTTP/3 200 \r\n</c>, then the body's
/// <c>DATA</c>, then zero once the stream has ended; trailers are kept in
/// <see cref="TrailerBytes" />.
/// </summary>
/// <remarks>
/// Failures are the ones curl 8.21.0's <c>cf-ngtcp2.c</c> reports (ADR-0144 section 7,
/// ADR-0172, ADR-0187): a stream the server resets is exit 95, or 18 once body bytes have
/// arrived, with <c>HTTP/3 stream &lt;id&gt; reset by server (error 0x&lt;hex&gt;
/// &lt;name&gt;)</c>; so is a response head with no valid <c>:status</c>, which this client
/// resets with <c>H3_MESSAGE_ERROR</c> as nghttp3 does. A reset with
/// <c>H3_REQUEST_REJECTED</c> stops the session taking new streams and is exit 56 marked
/// <see cref="HttpTransferException.IsStreamRefused" />, for the handler to send the request
/// again; a reset with <c>H3_NO_ERROR</c>, or with any other code after the final head when
/// no body is wanted, ends the stream as its end would. A stream that ends before the final
/// head is exit 95; frames or field sections that break RFC 9114 or RFC 9204 are exit 56 with
/// nghttp3's error name, as is a connection error on the server's control or QPACK streams
/// (<see cref="Http3Session.ConnectionError" />), which fails the next read and any read it
/// interrupts; and a lost connection is the exit code and message its
/// <see cref="MultiplexedConnectionFailedException" /> carries.
/// </remarks>
/// <param name="session">The connection's HTTP/3 session.</param>
/// <param name="scheme">The URL's scheme, sent as <c>:scheme</c>.</param>
/// <param name="bodyLength">The request body's length, 0 when there is none, or <see langword="null" /> when unknown.</param>
/// <param name="ignoresBody">
/// <see langword="true" /> when no response body is wanted (<c>-I</c>), so a reset after the
/// final head ends the stream instead of failing it.
/// </param>
internal sealed class Http3StreamConnection(Http3Session session, string scheme, long? bodyLength, bool ignoresBody) : IHttpStreamConnection
{
    /// <summary>
    /// The longest frame payload read off a request stream: <c>Curl.Http3</c> reads each
    /// frame whole, so a <c>DATA</c> frame is held in memory at once (ADR-0172).
    /// </summary>
    internal const long MaximumFramePayloadLength = 16 * 1024 * 1024;

    private readonly Queue<ReadOnlyMemory<byte>> received = new();

    private readonly MemoryStream trailers = new();

    private ReadOnlyMemory<byte> unread;

    private IMultiplexedStream? stream;

    private Http3FrameReader? frames;

    private long bodyBytesSent;

    private long bodyBytesReceived;

    private bool isRequestEnded;

    private bool isResponseEnded;

    private bool isFinalHeadReceived;

    /// <summary>Gets a value indicating that QUIC traffic is always encrypted.</summary>
    public bool IsSecure => true;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => session.RemoteEndPoint;

    /// <inheritdoc />
    public ReadOnlyMemory<byte> TrailerBytes => trailers.ToArray();

    /// <inheritdoc />
    /// <exception cref="HttpTransferException">The stream or connection failed (exit 18, 56, 95 or the connection's own).</exception>
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
    /// <exception cref="HttpTransferException">The connection was lost (the connection's own exit code).</exception>
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (stream is null)
        {
            await StartAsync(buffer, cancellationToken).ConfigureAwait(false);
            return;
        }

        await SendDataAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Does nothing: the QUIC stream sends what it is given.</summary>
    /// <param name="cancellationToken">Not used.</param>
    /// <returns>A completed task.</returns>
    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <summary>
    /// Ends the request stream with an empty write carrying FIN, unless it has ended already:
    /// it had no body, or the last body write ended it. Over HTTP/3 nothing is read while the
    /// body is sent, so the response cannot have ended yet.
    /// </summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the stream's end is sent.</returns>
    /// <exception cref="HttpTransferException">The connection was lost (the connection's own exit code).</exception>
    public async ValueTask EndRequestAsync(CancellationToken cancellationToken)
    {
        if (isRequestEnded)
        {
            return;
        }

        await WriteOnStreamAsync(ReadOnlyMemory<byte>.Empty, endStream: true, cancellationToken).ConfigureAwait(false);
        isRequestEnded = true;
    }

    /// <inheritdoc />
    /// <exception cref="HttpTransferException">The stream or connection failed (exit 18, 56, 95 or the connection's own).</exception>
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
    /// Does nothing: the session disposes every stream it opened when it is disposed.
    /// </summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static HttpTransferException Lost(MultiplexedConnectionFailedException lost) => new(lost.ExitCode, lost.Message);

    /// <summary>
    /// Gives nghttp3's name for an HTTP/3 error as <c>nghttp3_strerror</c> gives it, such as
    /// <c>ERR_H3_FRAME_UNEXPECTED</c> for <see cref="Http3ErrorCode.FrameUnexpected" />.
    /// </summary>
    /// <param name="errorCode">The HTTP/3 error.</param>
    /// <returns>nghttp3's name for it.</returns>
    internal static string Nghttp3ErrorName(Http3ErrorCode errorCode) => "ERR_H3_" + UpperSnakeCase(errorCode.ToString());

    /// <summary>
    /// Gives a Pascal-case enumeration member's name in nghttp3's upper snake case, such as
    /// <c>FRAME_UNEXPECTED</c> for <c>FrameUnexpected</c>.
    /// </summary>
    /// <param name="name">The member's name.</param>
    /// <returns>The name in upper snake case.</returns>
    internal static string UpperSnakeCase(string name) =>
        string.Concat(name.Select((letter, index) => index > 0 && char.IsUpper(letter) ? $"_{letter}" : $"{letter}")).ToUpperInvariant();

    private static HttpTransferException ReadStreamFailed(string errorName) =>
        new(CurlExitCode.RecvError, HttpTransferMessages.Http3ReadStreamFailed(errorName));

    private async ValueTask StartAsync(ReadOnlyMemory<byte> head, CancellationToken cancellationToken)
    {
        isRequestEnded = bodyLength == 0;
        try
        {
            stream = await session.OpenRequestStreamAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (MultiplexedConnectionFailedException lost)
        {
            throw Lost(lost);
        }

        frames = new Http3FrameReader(new MultiplexedStreamAdapter(stream), MaximumFramePayloadLength);
        byte[] section = session.Encoder.EncodeFieldSection(stream.StreamId, Http2RequestHeaders.Of(head.Span, scheme));
        await WriteOnStreamAsync(new Http3HeadersFrame(section).ToBytes(), isRequestEnded, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask SendDataAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        if (data.IsEmpty)
        {
            return;
        }

        bodyBytesSent += data.Length;
        bool isLast = bodyBytesSent == bodyLength;
        await WriteOnStreamAsync(new Http3DataFrame(data).ToBytes(), isLast, cancellationToken).ConfigureAwait(false);
        isRequestEnded = isLast;
    }

    private async ValueTask WriteOnStreamAsync(ReadOnlyMemory<byte> bytes, bool endStream, CancellationToken cancellationToken)
    {
        try
        {
            await stream!.WriteAsync(bytes, endStream, cancellationToken).ConfigureAwait(false);
        }
        catch (MultiplexedConnectionFailedException lost)
        {
            throw Lost(lost);
        }
    }

    private async ValueTask ReceiveFrameAsync(CancellationToken cancellationToken)
    {
        switch (await ReadFrameAsync(cancellationToken).ConfigureAwait(false))
        {
            case null:
                ReceiveEnd();
                break;
            case Http3HeadersFrame headers:
                ReceiveHeaders(headers);
                break;
            case Http3DataFrame data:
                ReceiveData(data);
                break;
            default:
                throw ReadStreamFailed(Nghttp3ErrorName(Http3ErrorCode.FrameUnexpected));
        }
    }

    /// <summary>
    /// Reads the next frame, unless a connection error on the server's control or QPACK
    /// streams failed the whole connection (<see cref="Http3Session.ConnectionError" />).
    /// </summary>
    private async ValueTask<Http3Frame?> ReadFrameAsync(CancellationToken cancellationToken)
    {
        if (session.ConnectionError is { } failed)
        {
            throw failed;
        }

        try
        {
            return await frames!.ReadFrameAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (session.ConnectionError is { } connectionError)
        {
            // The session closed the connection under the read: its error is the cause.
            throw connectionError;
        }
        catch (MultiplexedStreamResetException reset) when (reset.ApplicationErrorCode == (long)Http3ErrorCode.RequestRejected)
        {
            session.StopNewStreams();
            throw new HttpTransferException(CurlExitCode.RecvError, HttpTransferMessages.Http3StreamRefused(stream!.StreamId)) { IsStreamRefused = true };
        }
        catch (MultiplexedStreamResetException reset) when (reset.ApplicationErrorCode == (long)Http3ErrorCode.NoError || (isFinalHeadReceived && ignoresBody))
        {
            return null;
        }
        catch (MultiplexedStreamResetException reset)
        {
            throw Reset(reset.ApplicationErrorCode);
        }
        catch (MultiplexedConnectionFailedException lost)
        {
            throw Lost(lost);
        }
        catch (Http3Exception error)
        {
            throw ReadStreamFailed(Nghttp3ErrorName(error.ErrorCode));
        }
    }

    /// <summary>
    /// Gives the failure for a request stream reset with <paramref name="errorCode" />: exit
    /// 95, or exit 18 once body bytes have arrived, as <c>cf-ngtcp2.c</c> counts them.
    /// </summary>
    private HttpTransferException Reset(long errorCode) => new(
        bodyBytesReceived > 0 ? CurlExitCode.PartialFile : CurlExitCode.Http3,
        HttpTransferMessages.Http3StreamReset(stream!.StreamId, errorCode));

    private void ReceiveEnd()
    {
        if (!isFinalHeadReceived)
        {
            throw new HttpTransferException(CurlExitCode.Http3, HttpTransferMessages.Http3StreamClosedBeforeHead(stream!.StreamId));
        }

        isResponseEnded = true;
    }

    /// <summary>
    /// Takes a field section: a response head until the final one, then trailers.
    /// </summary>
    private void ReceiveHeaders(Http3HeadersFrame frame)
    {
        IReadOnlyList<HeaderField> fields = Decode(frame.EncodedFieldSection);
        if (isFinalHeadReceived)
        {
            trailers.Write(Http2ResponseHead.FormatTrailers(fields));
            return;
        }

        if (Http2ResponseHead.StatusOf(fields) is not { } statusCode)
        {
            stream!.Abort((long)Http3ErrorCode.MessageError);
            throw Reset((long)Http3ErrorCode.MessageError);
        }

        received.Enqueue(Http2ResponseHead.Format(session.VersionName, statusCode, fields));
        isFinalHeadReceived = statusCode >= 200;
    }

    private void ReceiveData(Http3DataFrame frame)
    {
        if (!isFinalHeadReceived)
        {
            throw ReadStreamFailed(Nghttp3ErrorName(Http3ErrorCode.FrameUnexpected));
        }

        received.Enqueue(frame.Payload);
        bodyBytesReceived += frame.Payload.Length;
    }

    /// <summary>
    /// Decodes a field section. It never blocks: the session advertises no dynamic table and
    /// no blocked streams, so a section that refers to the dynamic table fails instead.
    /// </summary>
    private IReadOnlyList<HeaderField> Decode(ReadOnlyMemory<byte> section)
    {
        try
        {
            _ = session.Decoder.TryDecodeFieldSection(stream!.StreamId, section.Span, out IReadOnlyList<HeaderField>? fields);
            return fields!;
        }
        catch (QpackException)
        {
            throw ReadStreamFailed("ERR_QPACK_DECOMPRESSION_FAILED");
        }
    }
}
