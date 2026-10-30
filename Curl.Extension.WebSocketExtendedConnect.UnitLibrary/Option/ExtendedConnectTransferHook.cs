using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.WebSocketExtendedConnect.Http2;
using Curl.Extension.WebSocketExtendedConnect.Http3;
using Curl.Protocol.Abstractions;
using Curl.WebSocket;

namespace Curl.Extension.WebSocketExtendedConnect.Option;

/// <summary>
/// Opens a ws or wss transfer's WebSocket with extended CONNECT over HTTP/2 (<c>h2</c>, RFC 8441) or HTTP/3
/// (<c>h3</c>, RFC 9220, wss only) on a connection from the host's connector, and runs it with
/// <see cref="ExtendedConnectSession" />; any other transfer goes on unchanged.
/// </summary>
/// <param name="version">The <c>--ws-extended-connect</c> argument: <c>h2</c> or <c>h3</c>.</param>
/// <param name="randomSource">The source of each sent frame's mask.</param>
internal sealed class ExtendedConnectTransferHook(string version, IWebSocketRandomSource randomSource) : ITransferHook
{
    /// <inheritdoc />
    public ValueTask<TransferResult> ExecuteAsync(ITransferContext context, TransferHookServices services, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        bool secure = context.Url.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase);
        if (!secure && !context.Url.Scheme.Equals("ws", StringComparison.OrdinalIgnoreCase))
        {
            return next(context);
        }

        return version switch
        {
            "h2" => new(OpenOverHttp2Async(context, services.Connector, secure)),
            "h3" => new(OpenOverHttp3Async(context, services.Connector, secure)),
            _ => ValueTask.FromResult(TransferResult.Failure(CurlExitCode.FailedInit, $"--ws-extended-connect takes h2 or h3, not \"{version}\"")),
        };
    }

    private async Task<TransferResult> OpenOverHttp2Async(ITransferContext context, IConnector connector, bool secure)
    {
        CurlUrl url = context.Url;
        ConnectResult connected = await connector.ConnectAsync(TargetOf(context, secure, "h2"), context.CancellationToken).ConfigureAwait(false);
        if (connected.Connection is not { } connection)
        {
            return TransferResult.Failure(connected.ExitCode, connected.ErrorMessage!);
        }

        await using (connection.ConfigureAwait(false))
        {
            if (secure && connected.ApplicationProtocol != "h2")
            {
                return TransferResult.Failure(CurlExitCode.Http2, $"The server at {url.Host} did not negotiate h2 with ALPN, so it cannot take an extended CONNECT over HTTP/2");
            }

            try
            {
                Http2WebSocket webSocket = await Http2WebSocketClient.OpenAsync(
                    new ConnectionStream(connection), SchemeOf(secure), PathOf(url), AuthorityOf(url), [], randomSource, context.CancellationToken).ConfigureAwait(false);
                return await ExtendedConnectSession.RunAsync(new Http2ExtendedConnectChannel(webSocket), context).ConfigureAwait(false);
            }
            catch (Http2WebSocketException failure)
            {
                return TransferResult.Failure(CurlExitCode.Http2, failure.Message);
            }
        }
    }

    private async Task<TransferResult> OpenOverHttp3Async(ITransferContext context, IConnector connector, bool secure)
    {
        if (!secure)
        {
            return TransferResult.Failure(CurlExitCode.UnsupportedProtocol, "--ws-extended-connect h3 needs a wss:// URL: HTTP/3 always runs over TLS");
        }

        CurlUrl url = context.Url;
        MultiplexedConnectResult connected = await connector.ConnectMultiplexedAsync(TargetOf(context, secure, "h3"), context.CancellationToken).ConfigureAwait(false);
        if (connected.Connection is not { } connection)
        {
            return TransferResult.Failure(connected.ExitCode, connected.ErrorMessage!);
        }

        try
        {
            Http3ExtendedConnectWebSocket webSocket = await Http3ExtendedConnectClient.OpenAsync(
                connection, SchemeOf(secure), PathOf(url), AuthorityOf(url), [], randomSource, context.CancellationToken).ConfigureAwait(false);
            return await RunAndDisposeAsync(webSocket, context).ConfigureAwait(false);
        }
        catch (Http3ExtendedConnectException failure)
        {
            return TransferResult.Failure(CurlExitCode.Http3, failure.Message);
        }
    }

    private static async Task<TransferResult> RunAndDisposeAsync(Http3ExtendedConnectWebSocket webSocket, ITransferContext context)
    {
        await using (webSocket.ConfigureAwait(false))
        {
            return await ExtendedConnectSession.RunAsync(new Http3ExtendedConnectChannel(webSocket), context).ConfigureAwait(false);
        }
    }

    private static ConnectTarget TargetOf(ITransferContext context, bool secure, string applicationProtocol) =>
        new(context.Url.Host, context.Url.Port, secure)
        {
            ApplicationProtocols = [applicationProtocol],
            Events = context.Events,
            DiagnosticLog = context.DiagnosticLog,
        };

    private static string SchemeOf(bool secure) => secure ? "https" : "http";

    private static string PathOf(CurlUrl url) => url.Query is { } query ? $"{url.AbsolutePath}?{query}" : url.AbsolutePath;

    private static string AuthorityOf(CurlUrl url) => url.IsDefaultPort ? url.Host : $"{url.Host}:{url.Port}";
}
