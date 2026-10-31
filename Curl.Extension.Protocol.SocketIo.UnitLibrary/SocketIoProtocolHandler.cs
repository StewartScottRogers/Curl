using Curl.Protocol.Abstractions;
using Curl.WebSocket;

namespace Curl.Extension.Protocol.SocketIo;

/// <summary>
/// Serves <c>socketio://host[:port]/namespace[?query]</c> and its TLS form <c>socketios://</c> (ADR-1035):
/// connects to the host's Engine.IO v4 WebSocket transport at <c>/socket.io/</c>, the URL's query
/// appended to the handshake's, joins the namespace the URL's path names, emits the <c>-d</c> JSON array
/// as an event, and writes each event received as one JSON array per line until the server disconnects.
/// </summary>
/// <param name="connector">Opens the connection, with TLS for <c>socketios</c>.</param>
/// <param name="randomSource">The source of the <c>Sec-WebSocket-Key</c> and each frame's mask.</param>
public sealed class SocketIoProtocolHandler(IConnector connector, IWebSocketRandomSource randomSource) : IProtocolHandler
{
    private const string SecureScheme = "socketios";

    private const int KeyLength = 16;

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes { get; } = ["socketio", SecureScheme];

    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            return await TransferAsync(context).ConfigureAwait(false);
        }
        catch (SocketIoException failure)
        {
            return TransferResult.Failure(failure.ExitCode, failure.Message);
        }
    }

    /// <summary>Gives the namespace a URL's path names: <c>/</c> for an empty path or <c>/</c>.</summary>
    /// <param name="url">The URL.</param>
    /// <returns>The namespace.</returns>
    internal static string NamespaceOf(CurlUrl url) =>
        url.AbsolutePath.Length > 1 ? url.AbsolutePath : SocketIoPacket.MainNamespace;

    private static int PortOf(CurlUrl url, bool isSecure) =>
        url.Port >= 0 ? url.Port : isSecure ? 443 : 80;

    private async ValueTask<TransferResult> TransferAsync(ITransferContext context)
    {
        string? emit = SocketIoEmit.From(context.PostData);
        CurlUrl url = context.Url;
        bool isSecure = string.Equals(url.Scheme, SecureScheme, StringComparison.OrdinalIgnoreCase);
        var target = new ConnectTarget(url.IdnHost, PortOf(url, isSecure), isSecure)
        {
            Proxy = context.Proxy,
            Events = context.Events,
            DiagnosticLog = context.DiagnosticLog,
        };
        ConnectResult connect = await connector.ConnectAsync(target, context.CancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return new TransferResult(connect.ExitCode, 0, connect.ErrorMessage) { IsConnectionRefused = connect.IsConnectionRefused };
        }

        context.Progress.ReportTransferStarted();
        TransferResult result = await ExchangeResultAsync(connection, context, emit).ConfigureAwait(false);
        await connection.DisposeAsync().ConfigureAwait(false);
        return result;
    }

    private async ValueTask<TransferResult> ExchangeResultAsync(IConnection connection, ITransferContext context, string? emit)
    {
        try
        {
            return TransferResult.Success(await ExchangeAsync(connection, context, emit).ConfigureAwait(false));
        }
        catch (SocketIoException failure)
        {
            return TransferResult.Failure(failure.ExitCode, failure.Message);
        }
    }

    private async ValueTask<long> ExchangeAsync(IConnection connection, ITransferContext context, string? emit)
    {
        byte[] keyBytes = new byte[KeyLength];
        randomSource.Fill(keyBytes);
        string key = Convert.ToBase64String(keyBytes);
        await connection.WriteAsync(SocketIoUpgrade.Request(context.Url, key), context.CancellationToken).ConfigureAwait(false);
        await connection.FlushAsync(context.CancellationToken).ConfigureAwait(false);
        ReadOnlyMemory<byte> afterHead = await SocketIoUpgrade.ReadReplyAsync(connection, key, context.CancellationToken).ConfigureAwait(false);
        var channel = new WebSocketMessageChannel(connection, randomSource, afterHead);
        return await new SocketIoSession(channel, NamespaceOf(context.Url), emit, context.Output).RunAsync(context.CancellationToken).ConfigureAwait(false);
    }
}
