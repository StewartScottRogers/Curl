using Curl.Protocol.Abstractions;
using Curl.Protocol.Ws;

namespace Curl.Engine;

/// <summary>
/// The handler a <c>CURLOPT_CONNECT_ONLY</c> 2 perform of a <c>ws</c> or <c>wss</c> URL dispatches to: it runs
/// <see cref="WsProtocolHandler.ConnectAndUpgradeOnlyAsync" /> and, on a <c>101</c>, gives the open connection and the
/// bytes that came with the reply head to <paramref name="webSocket" /> for <c>curl_ws_recv</c> and <c>curl_ws_send</c>
/// (ADR-0616).
/// </summary>
/// <param name="upgrader">Connects and upgrades.</param>
/// <param name="webSocket">Keeps the upgraded connection.</param>
internal sealed class WebSocketUpgradeOnlyHandler(WsProtocolHandler upgrader, ManagedEasyWebSocket webSocket) : IProtocolHandler
{
    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes => upgrader.SupportedSchemes;

    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        WsUpgradeOutcome outcome = await upgrader.ConnectAndUpgradeOnlyAsync(context).ConfigureAwait(false);
        if (outcome.Connection is { } upgraded)
        {
            await webSocket.OpenAsync(upgraded, outcome.AlreadyReceived).ConfigureAwait(false);
        }

        return outcome.Result;
    }
}
