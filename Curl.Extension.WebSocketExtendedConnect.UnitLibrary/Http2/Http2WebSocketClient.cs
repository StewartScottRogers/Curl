using Curl.Extension.WebSocketExtendedConnect.Wire;
using Curl.Http2;
using Curl.WebSocket;

namespace Curl.Extension.WebSocketExtendedConnect.Http2;

/// <summary>
/// Opens a WebSocket on an HTTP/2 stream with extended CONNECT (RFC 8441): waits for the server's
/// SETTINGS_ENABLE_CONNECT_PROTOCOL (section 3), sends the extended CONNECT on a new stream (section 4) and takes
/// a 2xx response as the open WebSocket (section 5).
/// </summary>
internal static class Http2WebSocketClient
{
    /// <summary>Opens the WebSocket over a new HTTP/2 connection on <paramref name="transport" />.</summary>
    /// <param name="transport">The byte stream the HTTP/2 connection runs on, already past any TLS handshake; the caller owns it.</param>
    /// <param name="scheme">The <c>:scheme</c>: <c>https</c> for wss, <c>http</c> for ws.</param>
    /// <param name="path">The <c>:path</c>, the request path with any query.</param>
    /// <param name="authority">The <c>:authority</c>, the host with any non-default port.</param>
    /// <param name="subprotocols">The WebSocket subprotocols offered, in order of preference.</param>
    /// <param name="randomSource">The source of each sent frame's mask.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The open WebSocket.</returns>
    /// <exception cref="Http2WebSocketException">
    /// The server did not advertise the setting, answered other than 2xx, answered with DATA, reset the stream
    /// or closed the connection before answering.
    /// </exception>
    internal static async Task<Http2WebSocket> OpenAsync(
        Stream transport,
        string scheme,
        string path,
        string authority,
        IReadOnlyList<string> subprotocols,
        IWebSocketRandomSource randomSource,
        CancellationToken cancellationToken)
    {
        var recorder = new PeerSettingsRecorder();
        var connection = new Http2Connection(transport) { FrameObserver = recorder };
        await connection.SendPrefaceAsync(cancellationToken).ConfigureAwait(false);
        await WaitForPeerSettingsAsync(connection, cancellationToken).ConfigureAwait(false);
        if (!EnableConnectProtocolSetting.IsEnabled(recorder.Settings))
        {
            throw new Http2WebSocketException(
                "The server did not advertise SETTINGS_ENABLE_CONNECT_PROTOCOL, so it cannot open a WebSocket over HTTP/2 (RFC 8441 section 3).");
        }

        var streamId = connection.OpenStream();
        var encoder = new HpackEncoder();
        encoder.SetPeerMaximumTableSize((int)Math.Min(connection.PeerSettings.HeaderTableSize, int.MaxValue));
        var request = encoder.Encode(ExtendedConnectRequestFields.Build(scheme, path, authority, subprotocols));
        await connection.WriteHeadersAsync(streamId, request, isEndStream: false, cancellationToken).ConfigureAwait(false);
        var response = await ReadResponseFieldsAsync(connection, cancellationToken).ConfigureAwait(false);
        if (!ExtendedConnectResponse.IsAccepted(response))
        {
            throw new Http2WebSocketException(
                $"The server refused the WebSocket: the extended CONNECT was answered with :status {ExtendedConnectResponse.StatusOf(response) ?? "(none)"}, not 2xx (RFC 8441 section 5).");
        }

        return new Http2WebSocket(connection, streamId, randomSource);
    }

    private static async Task WaitForPeerSettingsAsync(Http2Connection connection, CancellationToken cancellationToken)
    {
        while (!connection.IsPeerSettingsReceived)
        {
            _ = await connection.ReadFrameAsync(cancellationToken).ConfigureAwait(false);
            if (connection.IsClosedByPeer)
            {
                throw new Http2WebSocketException("The server closed the HTTP/2 connection before sending its SETTINGS (RFC 8441 section 3).");
            }
        }
    }

    private static async Task<IReadOnlyList<HeaderField>> ReadResponseFieldsAsync(Http2Connection connection, CancellationToken cancellationToken)
    {
        // Push is off and the extended CONNECT is the only stream, so the first stream frame answers it.
        var frame = await Http2WebSocket.ReadTranslatingResetAsync(connection.ReadStreamFrameAsync(cancellationToken)).ConfigureAwait(false)
            ?? throw new Http2WebSocketException("The server closed the HTTP/2 connection before answering the extended CONNECT (RFC 8441 section 5).");
        if (frame.Type != Http2FrameType.Headers)
        {
            throw new Http2WebSocketException("The server sent DATA before answering the extended CONNECT (RFC 8441 section 5).");
        }

        return new HpackDecoder().Decode(frame.Content.Span);
    }
}
