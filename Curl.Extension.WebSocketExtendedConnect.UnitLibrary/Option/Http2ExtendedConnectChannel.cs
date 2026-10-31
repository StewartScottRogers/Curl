using Curl.Extension.WebSocketExtendedConnect.Http2;
using Curl.WebSocket;

namespace Curl.Extension.WebSocketExtendedConnect.Option;

/// <summary>An <see cref="Http2WebSocket" /> as an <see cref="IExtendedConnectChannel" />.</summary>
/// <param name="webSocket">The open WebSocket.</param>
internal sealed class Http2ExtendedConnectChannel(Http2WebSocket webSocket) : IExtendedConnectChannel
{
    /// <inheritdoc />
    public Task SendAsync(WsOpcode opcode, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) =>
        webSocket.SendAsync(opcode, payload, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<WsFrameRun>?> ReceiveAsync(CancellationToken cancellationToken) =>
        webSocket.IsClosedByServer ? null : await webSocket.ReceiveAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task CloseAsync(CancellationToken cancellationToken) => webSocket.CloseAsync(cancellationToken);
}
