using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Http3;

/// <summary>
/// One HTTP/3 request stream of an <see cref="Http3ClientSession" />: it sends HEADERS built
/// from any field list the caller gives (pseudo-headers included, such as <c>:protocol</c> for
/// extended CONNECT), DATA, and the end of the request, and reads the response's frames and
/// DATA payload. A stream reset reaches the caller as <see cref="MultiplexedStreamResetException" />,
/// a framing error as <see cref="Http3Exception" />, and a read the session's connection error
/// interrupts as that error.
/// </summary>
public sealed class Http3ClientRequestStream
{
    /// <summary>The largest frame payload a response frame may carry.</summary>
    public const long MaximumFramePayloadLength = 16 * 1024 * 1024;

    private readonly Http3ClientSession session;
    private readonly Http3FrameReader frames;

    internal Http3ClientRequestStream(Http3ClientSession session, IMultiplexedStream stream)
    {
        this.session = session;
        Stream = stream;
        frames = new Http3FrameReader(new Http3MultiplexedStreamAdapter(stream), MaximumFramePayloadLength);
    }

    /// <summary>Gets the QUIC stream the request runs on.</summary>
    public IMultiplexedStream Stream { get; }

    /// <summary>Gets the QUIC stream ID.</summary>
    public long StreamId => Stream.StreamId;

    /// <summary>Encodes <paramref name="fields" /> with the session's QPACK encoder and sends them as one HEADERS frame.</summary>
    /// <param name="fields">The field lines in order, pseudo-headers first; none is added or checked.</param>
    /// <param name="endStream">Whether the frame ends the request.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The length of the encoded field section, the HEADERS frame's payload length.</returns>
    public async ValueTask<int> SendHeadersAsync(IReadOnlyList<HeaderField> fields, bool endStream, CancellationToken cancellationToken)
    {
        byte[] section = session.Encoder.EncodeFieldSection(StreamId, fields);
        await Stream.WriteAsync(new Http3HeadersFrame(section).ToBytes(), endStream, cancellationToken).ConfigureAwait(false);
        return section.Length;
    }

    /// <summary>Sends <paramref name="data" /> as one DATA frame.</summary>
    /// <param name="data">The payload.</param>
    /// <param name="endStream">Whether the frame ends the request.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the frame is written.</returns>
    public ValueTask SendDataAsync(ReadOnlyMemory<byte> data, bool endStream, CancellationToken cancellationToken) =>
        Stream.WriteAsync(new Http3DataFrame(data).ToBytes(), endStream, cancellationToken);

    /// <summary>Ends the request without another frame.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the end of the stream is written.</returns>
    public ValueTask EndRequestAsync(CancellationToken cancellationToken) =>
        Stream.WriteAsync(ReadOnlyMemory<byte>.Empty, endStream: true, cancellationToken);

    /// <summary>
    /// Reads the next response frame, or DATA payload into <paramref name="buffer" />, or the end
    /// of the stream. Throws the session's <see cref="Http3ClientSession.ConnectionError" /> when
    /// there is one before or during the read.
    /// </summary>
    /// <param name="buffer">Receives DATA payload bytes.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The frame, the count of DATA bytes read, or the end of the stream.</returns>
    public async ValueTask<Http3FrameOrData> ReadFrameOrDataAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (session.ConnectionError is { } failed)
        {
            throw failed;
        }

        try
        {
            return await frames.ReadFrameOrDataAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (session.ConnectionError is { } connectionError)
        {
            // The session closed the connection under the read: its error is the cause.
            throw connectionError;
        }
    }

    /// <summary>Decodes a response HEADERS frame's field section with the session's QPACK decoder.</summary>
    /// <param name="frame">The HEADERS frame read from this stream.</param>
    /// <returns>The field lines in order.</returns>
    public IReadOnlyList<HeaderField> DecodeFieldSection(Http3HeadersFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        _ = session.Decoder.TryDecodeFieldSection(StreamId, frame.EncodedFieldSection.Span, out IReadOnlyList<HeaderField>? fields);
        return fields!;
    }
}
