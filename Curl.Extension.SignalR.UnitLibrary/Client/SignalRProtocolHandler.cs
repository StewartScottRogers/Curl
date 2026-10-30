using Curl.Extension.SignalR.Transport;
using Curl.Protocol.Abstractions;
using Curl.WebSocket;

namespace Curl.Extension.SignalR.Client;

/// <summary>
/// Serves <c>signalr://host[:port]/hub[?query]</c> and its TLS form <c>signalrs://</c> (ADR-1084): negotiates with the
/// hub at <c>http://</c> (or <c>https://</c>) the same host, port, path and query (TransportProtocols.md), opens the
/// WebSocket transport it offers and runs <see cref="SignalRHubSession"/> with the <c>-d</c> call.
/// </summary>
/// <param name="connector">Opens each connection, with TLS for <c>https</c> and <c>wss</c>.</param>
/// <param name="randomSource">The source of the <c>Sec-WebSocket-Key</c> and each frame's mask.</param>
public sealed class SignalRProtocolHandler(IConnector connector, IWebSocketRandomSource randomSource) : IProtocolHandler
{
    private const string SecureScheme = "signalrs";

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes { get; } = ["signalr", SecureScheme];

    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            return TransferResult.Success(await TransferAsync(context).ConfigureAwait(false));
        }
        catch (SignalRConnectException failure)
        {
            return new TransferResult(failure.Result.ExitCode, 0, failure.Result.ErrorMessage) { IsConnectionRefused = failure.Result.IsConnectionRefused };
        }
        catch (SignalRHubException failure)
        {
            return TransferResult.Failure(failure.ExitCode, failure.Message);
        }
        catch (Exception failure) when (failure is SignalRTransportException or SignalRWireFormatException)
        {
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, $"SignalR: {failure.Message}");
        }
    }

    /// <summary>
    /// Gives the hub's endpoint URL for <paramref name="url"/>: <c>http</c> for <c>signalr</c> and <c>https</c> for
    /// <c>signalrs</c>, port 80 or 443 unless the URL names one, with the URL's path and query.
    /// </summary>
    internal static Uri EndpointUrlOf(CurlUrl url)
    {
        bool isSecure = string.Equals(url.Scheme, SecureScheme, StringComparison.OrdinalIgnoreCase);
        int port = url.Port >= 0 ? url.Port : isSecure ? 443 : 80;
        var builder = new UriBuilder(isSecure ? Uri.UriSchemeHttps : Uri.UriSchemeHttp, url.IdnHost, port, url.RequestPath);
        if (url.Query is { } query)
        {
            builder.Query = query;
        }

        return builder.Uri;
    }

    private async Task<long> TransferAsync(ITransferContext context)
    {
        byte[]? callRecord = SignalRCall.RecordFor(context.PostData);
        var negotiator = new SignalRNegotiator((url, cancellationToken) => OpenAsync(url, context, cancellationToken));
        NegotiatedWebSocketEndpoint endpoint = await negotiator.NegotiateAsync(EndpointUrlOf(context.Url), context.CancellationToken);
        await using IConnection connection = await OpenAsync(endpoint.WebSocketUrl, context, context.CancellationToken);
        SignalRWebSocketSession session = await SignalRWebSocketUpgrade.UpgradeAsync(
            connection, endpoint.WebSocketUrl, endpoint.AccessToken, randomSource, context.CancellationToken);
        context.Progress.ReportTransferStarted();
        return await new SignalRHubSession(session, callRecord, context.Output).RunAsync(context.CancellationToken);
    }

    private async ValueTask<IConnection> OpenAsync(Uri url, ITransferContext context, CancellationToken cancellationToken)
    {
        var target = new ConnectTarget(url.IdnHost, url.Port, url.Scheme is "https" or "wss")
        {
            Proxy = context.Proxy,
            Events = context.Events,
            DiagnosticLog = context.DiagnosticLog,
        };
        ConnectResult result = await connector.ConnectAsync(target, cancellationToken).ConfigureAwait(false);
        return result.Connection ?? throw new SignalRConnectException(result);
    }
}
