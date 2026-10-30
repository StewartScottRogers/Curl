using Curl.Extension.WebSocketExtendedConnect.Http3;
using Curl.WebSocket;

namespace Curl.Extension.WebSocketExtendedConnect.Option;

/// <summary>An <see cref="Http3ExtendedConnectWebSocket" /> as an <see cref="IExtendedConnectChannel" />.</summary>
/// <param name="webSocket">The open WebSocket.</param>
internal sealed class Http3ExtendedConnectChannel(Http3ExtendedConnectWebSocket webSocket) : IExtendedConnectChannel
{
    /// <inheritdoc />
    public async Task SendAsync(WsOpcode opcode, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) =>
        await webSocket.SendFrameAsync(opcode, isFinal: true, payload, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<WsFrameRun>?> ReceiveAsync(CancellationToken cancellationToken) =>
        await webSocket.ReceiveFramesAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task CloseAsync(CancellationToken cancellationToken) =>
        await webSocket.CloseAsync(cancellationToken).ConfigureAwait(false);
}
