using Curl.Extension.WebSocketExtendedConnect.Wire;
using Curl.Http2;
using Curl.Http3;
using Curl.Protocol.Abstractions;
using Curl.WebSocket;

namespace Curl.Extension.WebSocketExtendedConnect.Http3;

/// <summary>
/// Opens a WebSocket on an HTTP/3 connection with extended CONNECT (RFC 9220 section 3): it waits for the server's
/// SETTINGS, requires SETTINGS_ENABLE_CONNECT_PROTOCOL of 1, sends the extended CONNECT on a new request stream and
/// takes a 2xx response as the open WebSocket.
/// </summary>
internal static class Http3ExtendedConnectClient
{
    private const int DiscardBufferLength = 1024;

    /// <summary>
    /// Opens the WebSocket. The session it starts on <paramref name="connection" /> belongs to the returned WebSocket,
    /// which disposes it; on failure it is disposed before the exception leaves.
    /// </summary>
    /// <param name="connection">The QUIC connection, whose application protocol is <c>h3</c>.</param>
    /// <param name="scheme">The <c>:scheme</c>: <c>https</c> for wss, <c>http</c> for ws.</param>
    /// <param name="path">The <c>:path</c>, the request path with any query.</param>
    /// <param name="authority">The <c>:authority</c>, the host with any non-default port.</param>
    /// <param name="subprotocols">The WebSocket subprotocols offered, in order of preference.</param>
    /// <param name="randomSource">The source of the masking keys of the frames the client sends.</param>
    /// <param name="cancellationToken">Cancels the opening.</param>
    /// <returns>The open WebSocket.</returns>
    /// <exception cref="Http3ExtendedConnectException">
    /// The server did not enable extended CONNECT, ended the stream before responding, or answered other than 2xx.
    /// </exception>
    internal static async ValueTask<Http3ExtendedConnectWebSocket> OpenAsync(IMultiplexedConnection connection, string scheme, string path, string authority, IReadOnlyList<string> subprotocols, IWebSocketRandomSource randomSource, CancellationToken cancellationToken)
    {
        TaskCompletionSource<Http3SettingsFrame> settingsArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Http3ClientSession session = new(connection, [], frame => ReportSettings(frame, settingsArrived));
        Task<Http3ExtendedConnectWebSocket> opening = OpenOnSessionAsync(session, settingsArrived.Task, ExtendedConnectRequestFields.Build(scheme, path, authority, subprotocols), randomSource, cancellationToken);
        await ((Task)opening).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (!opening.IsCompletedSuccessfully)
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }

        return await opening.ConfigureAwait(false);
    }

    private static async Task<Http3ExtendedConnectWebSocket> OpenOnSessionAsync(Http3ClientSession session, Task<Http3SettingsFrame> settingsArrived, IReadOnlyList<HeaderField> request, IWebSocketRandomSource randomSource, CancellationToken cancellationToken)
    {
        await RequireExtendedConnectAsync(session, settingsArrived, cancellationToken).ConfigureAwait(false);
        Http3ClientRequestStream stream = await session.OpenRequestStreamAsync(null, cancellationToken).ConfigureAwait(false);
        _ = await stream.SendHeadersAsync(request, endStream: false, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<HeaderField> response = await ReadFinalResponseAsync(stream, cancellationToken).ConfigureAwait(false);
        if (!ExtendedConnectResponse.IsAccepted(response))
        {
            throw new Http3ExtendedConnectException($"The server refused the WebSocket: the extended CONNECT got :status {ExtendedConnectResponse.StatusOf(response) ?? "(none)"}, not 2xx (RFC 9220 section 3).");
        }

        string? subprotocol = response.FirstOrDefault(field => field.Name == "sec-websocket-protocol").Value;
        return new Http3ExtendedConnectWebSocket(session, stream, subprotocol, randomSource);
    }

    private static void ReportSettings(Http3Frame frame, TaskCompletionSource<Http3SettingsFrame> settingsArrived)
    {
        if (frame is Http3SettingsFrame settings)
        {
            _ = settingsArrived.TrySetResult(settings);
        }
    }

    // RFC 9220 section 3: the client waits for the server's SETTINGS before sending an extended CONNECT.
    private static async Task RequireExtendedConnectAsync(Http3ClientSession session, Task<Http3SettingsFrame> settingsArrived, CancellationToken cancellationToken)
    {
        _ = await Task.WhenAny(settingsArrived, session.PeerStreamsReading).WaitAsync(cancellationToken).ConfigureAwait(false);
        Http3SettingsFrame? settings = settingsArrived.IsCompleted ? await settingsArrived.ConfigureAwait(false) : null;
        if (settings is null || !EnableConnectProtocolSetting.IsEnabled(settings.Settings))
        {
            throw new Http3ExtendedConnectException("The server does not allow a WebSocket over HTTP/3: its SETTINGS carry no SETTINGS_ENABLE_CONNECT_PROTOCOL of 1 (RFC 9220 section 3).", session.ConnectionError);
        }
    }

    // Interim 1xx responses come before the final one (RFC 9114 section 4.1); DATA before any response is dropped.
    private static async ValueTask<IReadOnlyList<HeaderField>> ReadFinalResponseAsync(Http3ClientRequestStream stream, CancellationToken cancellationToken)
    {
        byte[] discarded = new byte[DiscardBufferLength];
        while (true)
        {
            Http3FrameOrData read = await stream.ReadFrameOrDataAsync(discarded, cancellationToken).ConfigureAwait(false);
            if (read.IsEndOfStream)
            {
                throw new Http3ExtendedConnectException("The server ended the extended CONNECT stream before it responded (RFC 9220 section 3).");
            }

            if (read.Frame is Http3HeadersFrame headers && stream.DecodeFieldSection(headers) is { } fields && ExtendedConnectResponse.StatusOf(fields)?.StartsWith('1') != true)
            {
                return fields;
            }
        }
    }
}
