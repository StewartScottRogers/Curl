using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.WebSocketRepl.Handshake;
using Curl.Extension.WebSocketRepl.Session;
using Curl.Protocol.Abstractions;
using Curl.WebSocket;

namespace Curl.Extension.WebSocketRepl.Transfer;

/// <summary>
/// Takes over a <c>ws://</c> or <c>wss://</c> transfer when <c>--ws-repl</c> is given (ADR-1081): connects through
/// curl's connector (TLS for <c>wss</c>, through the transfer's proxy), performs the opening handshake (RFC 6455
/// section 4.1) and runs a <see cref="WebSocketReplSession" /> until it closes. curl's own handler never sees the
/// transfer. Any other scheme's transfer goes on unchanged.
/// </summary>
/// <remarks>
/// A failed connect fails with the connector's exit code, a refused handshake with
/// <see cref="CurlExitCode.WeirdServerReply" />, and a server that breaks RFC 6455's framing rules with
/// <see cref="CurlExitCode.RecvError" />. A session that closes or whose connection ends succeeds.
/// </remarks>
/// <param name="input">The typed lines: standard input in the console.</param>
/// <param name="randomSource">Supplies the handshake key and each frame's mask.</param>
internal sealed class WebSocketReplTransferHook(TextReader input, IWebSocketRandomSource randomSource) : ITransferHook
{
    /// <summary>The failure when the host gives no connector.</summary>
    public const string NoServicesMessage = "--ws-repl needs curl's connector, which this transfer was not given.";

    /// <inheritdoc />
    public ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next) =>
        ValueTask.FromResult(TransferResult.Failure(CurlExitCode.FailedInit, NoServicesMessage));

    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(
        ITransferContext context, TransferHookServices services, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(next);
        CurlUrl url = context.Url;
        bool isSecure = IsScheme(url, "wss");
        if (!isSecure && !IsScheme(url, "ws"))
        {
            return await next(context).ConfigureAwait(false);
        }

        ConnectResult connect = await services.Connector
            .ConnectAsync(new ConnectTarget(url.IdnHost, url.Port, isSecure) { Proxy = context.Proxy }, context.CancellationToken)
            .ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return TransferResult.Failure(connect.ExitCode, $"Could not connect to {url.Host}: {connect.ErrorMessage}");
        }

        await using (connection.ConfigureAwait(false))
        {
            return await RunSessionAsync(connection, context).ConfigureAwait(false);
        }
    }

    /// <summary>Gives the <c>Host</c> value: the host, and the port when it is not the scheme's default.</summary>
    /// <param name="url">The transfer's URL.</param>
    /// <returns>The authority.</returns>
    internal static string AuthorityOf(CurlUrl url) => url.IsDefaultPort ? url.Host : $"{url.Host}:{url.Port}";

    /// <summary>Gives the request target: the path as curl sends it, and the query when the URL has one.</summary>
    /// <param name="url">The transfer's URL.</param>
    /// <returns>The path and query.</returns>
    internal static string PathAndQueryOf(CurlUrl url) => url.Query is { } query ? $"{url.RequestPath}?{query}" : url.RequestPath;

    private static bool IsScheme(CurlUrl url, string scheme) => string.Equals(url.Scheme, scheme, StringComparison.OrdinalIgnoreCase);

    private async Task<TransferResult> RunSessionAsync(IConnection connection, ITransferContext context)
    {
        WebSocketHandshakeResult handshake = await new WebSocketOpeningHandshake(randomSource)
            .PerformAsync(connection, AuthorityOf(context.Url), PathAndQueryOf(context.Url), context.CancellationToken)
            .ConfigureAwait(false);
        if (handshake.Failure is { } failure)
        {
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, $"Refused WebSocket upgrade: {failure}");
        }

        using WebSocketReplSession session = new(connection, randomSource);
        WebSocketReplOutcome outcome = await session
            .RunAsync(input, context.Output, handshake.AlreadyReceived, context.CancellationToken)
            .ConfigureAwait(false);
        return outcome.Ending == WebSocketReplEnding.ProtocolViolation
            ? TransferResult.Failure(CurlExitCode.RecvError, $"WebSocket protocol violation: {outcome.ViolationMessage}")
            : TransferResult.Success(0);
    }
}
