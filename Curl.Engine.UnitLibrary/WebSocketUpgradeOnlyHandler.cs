using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ws;

namespace Curl.Engine;

/// <summary>
/// The handler a <c>CURLOPT_CONNECT_ONLY</c> 2 perform of a <c>ws</c> or <c>wss</c> URL dispatches to: it runs
/// <see cref="WsProtocolHandler.ConnectAndUpgradeOnlyAsync" /> and, on a <c>101</c>, gives the open connection and the
/// bytes that came with the reply head to <paramref name="webSocket" /> for <c>curl_ws_recv</c> and <c>curl_ws_send</c>
/// (ADR-0616). A server that closes before the reply head ends is no failure here: the perform answers
/// <see cref="CurlExitCode.Ok" /> with no upgraded connection, as curl 8.22.0's <c>Curl_http_done</c> skips its
/// <c>Empty reply from server</c> check under <c>connect_only</c> (upstream test2304, ADR-0705).
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

        return outcome.Result.ExitCode == CurlExitCode.GotNothing
            ? TransferResult.Success(0) with { Report = outcome.Result.Report }
            : outcome.Result;
    }
}
