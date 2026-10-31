using Curl.WebSocket;

namespace Curl.Extension.WebSocketExtendedConnect.Option;

/// <summary>An open extended CONNECT WebSocket, over HTTP/2 or HTTP/3, as <see cref="ExtendedConnectSession" /> runs it.</summary>
internal interface IExtendedConnectChannel
{
    /// <summary>Sends one whole frame.</summary>
    /// <param name="opcode">The frame's opcode.</param>
    /// <param name="payload">The frame's payload.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that completes when the frame is sent.</returns>
    Task SendAsync(WsOpcode opcode, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);

    /// <summary>Receives the frame runs the next read gives.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The runs, or <see langword="null" /> once the server has ended its side of the stream.</returns>
    Task<IReadOnlyList<WsFrameRun>?> ReceiveAsync(CancellationToken cancellationToken);

    /// <summary>Ends the client's side of the stream.</summary>
    /// <param name="cancellationToken">Cancels the close.</param>
    /// <returns>A task that completes when the stream's end is sent.</returns>
    Task CloseAsync(CancellationToken cancellationToken);
}
