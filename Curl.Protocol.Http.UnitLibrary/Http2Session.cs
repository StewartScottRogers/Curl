using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// One connection speaking HTTP/2 for the HTTP handler: the frame layer over the
/// <see cref="IConnection" /> and the two HPACK contexts every stream on it shares. The client
/// preface goes out with the first request, as curl 8.21.0 sends it before the first HEADERS
/// without waiting for the server's SETTINGS (measured, BL-658 Notes).
/// </summary>
/// <param name="connection">The connection; the handler keeps ownership.</param>
internal sealed class Http2Session(IConnection connection)
{
    private readonly HpackEncoder encoder = new();

    private uint peerHeaderTableSize = HpackEncoder.DefaultMaximumTableSize;

    private bool isPrefaceSent;

    /// <summary>Gets the connection the session runs on.</summary>
    internal IConnection Connection { get; } = connection;

    /// <summary>Gets the frame layer.</summary>
    internal Http2Connection Frames { get; } = new(new HttpConnectionStream(connection));

    /// <summary>Gets the decoder every response header block on the connection passes through.</summary>
    internal HpackDecoder Decoder { get; } = new();

    /// <summary>
    /// Gets a value indicating whether the connection can carry another request: the peer has
    /// sent no GOAWAY and has not closed it.
    /// </summary>
    internal bool AcceptsNewStreams => Frames.PeerGoAway is null && !Frames.IsClosedByPeer;

    /// <summary>
    /// Creates the stream a request is sent and its response read on; nothing is sent until
    /// its head is written.
    /// </summary>
    /// <param name="scheme">The URL's scheme, sent as <c>:scheme</c>.</param>
    /// <param name="bodyLength">
    /// The request body's length, 0 when there is none, or <see langword="null" /> when it is
    /// unknown.
    /// </param>
    /// <returns>The stream.</returns>
    internal Http2StreamConnection CreateStream(string scheme, long? bodyLength) => new(this, scheme, bodyLength);

    /// <summary>
    /// Sends the client preface if it has not gone yet, then opens a stream and sends
    /// <paramref name="fields" /> on it as one header block.
    /// </summary>
    /// <param name="fields">The request's header list.</param>
    /// <param name="isEndStream">Whether the request has no body.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>The stream's identifier.</returns>
    internal async ValueTask<int> StartStreamAsync(IReadOnlyList<HeaderField> fields, bool isEndStream, CancellationToken cancellationToken)
    {
        if (!isPrefaceSent)
        {
            await Frames.SendPrefaceAsync(cancellationToken).ConfigureAwait(false);
            isPrefaceSent = true;
        }

        int streamId = Frames.OpenStream();
        await Frames.WriteHeadersAsync(streamId, Encode(fields), isEndStream, cancellationToken).ConfigureAwait(false);
        return streamId;
    }

    /// <summary>
    /// Encodes a header list, first taking a SETTINGS_HEADER_TABLE_SIZE the peer changed since
    /// the last block, so the block opens with the size update it calls for.
    /// </summary>
    private byte[] Encode(IReadOnlyList<HeaderField> fields)
    {
        uint headerTableSize = Frames.PeerSettings.HeaderTableSize;
        if (headerTableSize != peerHeaderTableSize)
        {
            encoder.SetPeerMaximumTableSize((int)Math.Min(headerTableSize, int.MaxValue));
            peerHeaderTableSize = headerTableSize;
        }

        return encoder.Encode(fields);
    }
}
