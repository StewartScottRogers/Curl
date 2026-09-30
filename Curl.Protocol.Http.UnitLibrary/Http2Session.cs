using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// One connection speaking HTTP/2 for the HTTP handler: the frame layer over the
/// <see cref="IConnection" /> and the two HPACK contexts every stream on it shares. The client
/// preface goes out with the first request, as curl 8.21.0 sends it before the first HEADERS
/// without waiting for the server's SETTINGS (measured, BL-658 Notes).
/// </summary>
/// <remarks>
/// The session outlives its transfer when the connection is pooled (BL-817, ADR-0159 point 5):
/// the handler hands it to the connection (<see cref="IConnection.TryHoldSession" />) and the
/// next transfer to the origin continues it - the next odd stream, the same HPACK tables, no
/// second preface. It keeps reading and writing through the connection it was created on,
/// whose later leases reach the same socket. Whoever closes the connection first calls
/// <see cref="ShutDownAsync" />, which sends curl's closing GOAWAY.
/// </remarks>
/// <param name="connection">The connection; the handler keeps ownership.</param>
internal sealed class Http2Session(IConnection connection) : IHttpStreamSession, IConnectionSession
{
    /// <summary>
    /// The receive window curl grows each stream to right after its HEADERS: 10 MiB
    /// (nghttp2's <c>H2_STREAM_WINDOW_SIZE_MAX</c> in curl; measured, BL-817 Notes).
    /// </summary>
    internal const int StreamReceiveWindowSize = 10 * 1024 * 1024;

    /// <summary>
    /// How many times curl 8.18.0 sends the stream's WINDOW_UPDATE after HEADERS, each with
    /// the same increment: twice (measured, BL-817 Notes).
    /// </summary>
    private const int StreamWindowUpdateCount = 2;

    /// <summary>The debug data of curl's closing GOAWAY: <c>shutdown</c> and its terminating NUL (measured, BL-817 Notes).</summary>
    private static readonly byte[] ShutdownDebugData = "shutdown\0"u8.ToArray();

    private readonly HpackEncoder encoder = new();

    private uint peerHeaderTableSize = HpackEncoder.DefaultMaximumTableSize;

    private bool isPrefaceSent;

    /// <summary>
    /// Gets the <c>HTTP2-Settings</c> value an h2c upgrade request carries: the payload of the
    /// SETTINGS frame the preface sends (<see cref="Http2Connection.ClientSettings" />) in
    /// base64url with no padding, <c>AAMAAABkAAQAAQAAAAIAAAAA</c> as curl sends it (measured,
    /// BL-716 Notes).
    /// </summary>
    internal static string UpgradeSettings { get; } = Convert.ToBase64String(Http2FrameFactory.CreateSettings(Http2Connection.ClientSettings).Payload.Span)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    /// <summary>Gets the connection the session runs on.</summary>
    internal IConnection Connection { get; } = connection;

    /// <summary>Gets the frame layer.</summary>
    internal Http2Connection Frames { get; } = new(new HttpConnectionStream(connection));

    /// <summary>Gets the decoder every response header block on the connection passes through.</summary>
    internal HpackDecoder Decoder { get; } = new();

    /// <inheritdoc />
    public string VersionName => "HTTP/2";

    /// <inheritdoc />
    public string UsingLine => HttpConnectionInfoLines.UsingHttp2;

    /// <summary>
    /// Gets a value indicating whether the connection can carry another request: the peer has
    /// sent no GOAWAY and has not closed it.
    /// </summary>
    public bool AcceptsNewStreams => Frames.PeerGoAway is null && !Frames.IsClosedByPeer;

    /// <summary>
    /// Creates the stream a request is sent and its response read on; nothing is sent until
    /// its head is written.
    /// </summary>
    /// <param name="scheme">The URL's scheme, sent as <c>:scheme</c>.</param>
    /// <param name="bodyLength">
    /// The request body's length, 0 when there is none, or <see langword="null" /> when it is
    /// unknown.
    /// </param>
    /// <param name="ignoresBody">Not used: HTTP/2 fails a reset stream whatever the request wanted.</param>
    /// <returns>The stream.</returns>
    public IHttpStreamConnection CreateStream(string scheme, long? bodyLength, bool ignoresBody) => new Http2StreamConnection(this, scheme, bodyLength);

    /// <summary>
    /// Sends GOAWAY with NO_ERROR, last stream 0 and debug data <c>shutdown</c> and a NUL, as
    /// curl 8.18.0 does when it closes an HTTP/2 connection (measured, BL-817 Notes); nothing
    /// when no preface went out, when a GOAWAY already has, or when the peer closed the
    /// connection. A connection that fails the write is closing anyway, so that is not an error.
    /// </summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the frame is written, or at once when none is due.</returns>
    public async ValueTask ShutDownAsync(CancellationToken cancellationToken)
    {
        if (!isPrefaceSent || Frames.IsGoAwaySent || Frames.IsClosedByPeer)
        {
            return;
        }

        try
        {
            await Frames.SendGoAwayAsync(Http2ErrorCode.NoError, ShutdownDebugData, cancellationToken).ConfigureAwait(false);
            await Connection.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // The transport is already gone; there is no one left to say goodbye to.
        }
    }

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
    /// Sends the client preface and opens stream 1 without sending anything on it: the stream
    /// an HTTP/1.1 request upgraded to h2c becomes, half closed by the client, whose response
    /// arrives on it (RFC 7540 section 3.2), as curl sends the preface once the <c>101</c> has
    /// arrived (measured, BL-716 Notes). Once the response ends the stream is closed, so later
    /// requests on the session open streams 3, 5, ... within the peer's concurrency limit (BL-866).
    /// </summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The stream's identifier, 1.</returns>
    internal async ValueTask<int> StartUpgradedStreamAsync(CancellationToken cancellationToken)
    {
        await Frames.SendPrefaceAsync(cancellationToken).ConfigureAwait(false);
        isPrefaceSent = true;
        return Frames.OpenUpgradedStream();
    }

    /// <summary>
    /// Sends curl's WINDOW_UPDATEs for a new stream. The increment is what takes the stream's
    /// window to <see cref="StreamReceiveWindowSize" /> from the initial size in force: the
    /// default 65535 until the peer acknowledges this client's SETTINGS, 65536 after, as
    /// nghttp2 counts it - 10420225 on the first stream of a prior-knowledge connection,
    /// 10420224 on the next (measured, BL-817 Notes).
    /// </summary>
    /// <param name="streamId">The stream whose window grows.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>A task that completes when both frames are written.</returns>
    internal async ValueTask GrowStreamReceiveWindowAsync(int streamId, CancellationToken cancellationToken)
    {
        int initialWindowSize = Frames.IsClientSettingsAcknowledged ? Http2Connection.ClientInitialWindowSize : Http2Settings.DefaultInitialWindowSize;
        for (int sent = 0; sent < StreamWindowUpdateCount; sent++)
        {
            await Frames.IncreaseStreamReceiveWindowAsync(streamId, StreamReceiveWindowSize - initialWindowSize, cancellationToken).ConfigureAwait(false);
        }
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
