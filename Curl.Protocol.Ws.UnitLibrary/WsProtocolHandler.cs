using System.Globalization;
using System.Net;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Ws;

/// <summary>
/// Serves the <c>ws</c> and <c>wss</c> schemes: connects, sends curl's HTTP/1.1 upgrade request
/// and reads the reply head, accepting the upgrade on a <c>101</c> (ADR-0128).
/// </summary>
/// <param name="connector">
/// Supplies the connection to the URL's host and port, over TLS for <c>wss</c> and tunnelled
/// through <see cref="ITransferContext.Proxy" /> when one is set. No
/// <see cref="System.Net.Sockets.Socket" /> is ever constructed here.
/// </param>
/// <param name="authenticator">
/// Builds the pre-emptive <c>Authorization</c> value for <c>-u</c>, <c>--basic</c> and
/// <c>--oauth2-bearer</c>, with no challenges.
/// </param>
/// <param name="randomSource">Supplies the 16 bytes behind <c>Sec-WebSocket-Key</c>.</param>
/// <remarks>
/// Matches curl 8.21.0, measured against a loopback listener (ADR-0128, BL-580). Any status
/// but <c>101</c> fails with exit 22, <c>Refused WebSocket upgrade: &lt;code&gt;</c>, and
/// nothing written to the output; <c>Sec-WebSocket-Accept</c>, <c>Upgrade</c> and
/// <c>Connection</c> in a <c>101</c> are not checked, because curl does not check them. The
/// reply head, <c>101</c> or not, is written to <see cref="ITransferContext.HeaderOutput" />
/// (<c>-D</c>). A connect failure is returned as the connector reported it.
/// </remarks>
public sealed class WsProtocolHandler(
    IConnector connector,
    IHttpAuthenticator authenticator,
    IWebSocketRandomSource randomSource) : IProtocolHandler
{
    /// <summary>The status code that accepts the upgrade.</summary>
    internal const int SwitchingProtocols = 101;

    /// <summary>How many random bytes <c>Sec-WebSocket-Key</c> encodes.</summary>
    private const int KeyLength = 16;

    /// <summary>
    /// The two schemes this handler serves, as curl 8.21.0's <c>--version</c> protocol list
    /// names them.
    /// </summary>
    private static readonly string[] Schemes = ["ws", "wss"];

    private readonly IConnector connector =
        connector ?? throw new ArgumentNullException(nameof(connector));

    private readonly IHttpAuthenticator authenticator =
        authenticator ?? throw new ArgumentNullException(nameof(authenticator));

    private readonly IWebSocketRandomSource randomSource =
        randomSource ?? throw new ArgumentNullException(nameof(randomSource));

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes => Schemes;

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <see cref="ITransferContext.CancellationToken" /> was cancelled.
    /// </exception>
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        CurlUrl url = context.Url;
        var target = new ConnectTarget(url.IdnHost, url.Port, url.Scheme == "wss")
        {
            Proxy = context.Proxy,
            Events = context.Events,
        };
        ConnectResult connect = await connector.ConnectAsync(target, context.CancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return new TransferResult(connect.ExitCode, 0, connect.ErrorMessage) { IsConnectionRefused = connect.IsConnectionRefused };
        }

        await using (connection.ConfigureAwait(false))
        {
            try
            {
                return await UpgradeAsync(connection, context).ConfigureAwait(false);
            }
            catch (WsTransferException failure)
            {
                return TransferResult.Failure(failure.ExitCode, failure.Message);
            }
        }
    }

    private async Task<TransferResult> UpgradeAsync(IConnection connection, ITransferContext context)
    {
        HttpRequestOptions options = context.Http ?? new HttpRequestOptions();
        string method = options.CustomMethod ?? "GET";
        string? authorization = authenticator.CreateAuthorization(
            new HttpAuthRequest(
                method,
                context.Url,
                WsUpgradeRequestFormatter.RequestTarget(context.Url),
                context.Credentials,
                options.BearerToken,
                options.AuthSchemes,
                IsProxy: false),
            []);
        byte[] request = WsUpgradeRequestFormatter.Format(context.Url, options, method, NewKey(), authorization);
        await SendAsync(connection, request, context.CancellationToken).ConfigureAwait(false);
        WsUpgradeResponse response = await WsUpgradeResponseReader.ReadAsync(connection, context.CancellationToken).ConfigureAwait(false);
        await WriteHeadAsync(context.HeaderOutput, response.Head, context.CancellationToken).ConfigureAwait(false);
        TransferReport report = new() { ResponseCode = response.StatusCode, HttpVersion = HttpVersion.Version11, Method = method };
        if (response.StatusCode != SwitchingProtocols)
        {
            string message = string.Create(CultureInfo.InvariantCulture, $"Refused WebSocket upgrade: {response.StatusCode}");
            return TransferResult.Failure(CurlExitCode.HttpReturnedError, message) with { Report = report };
        }

        return TransferResult.Success(0) with { Report = report };
    }

    /// <summary>Sends <paramref name="bytes" /> and flushes, turning a failure into curl's exit 55.</summary>
    /// <param name="connection">The connection to send on.</param>
    /// <param name="bytes">The request or frame to send.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that completes once the bytes are flushed.</returns>
    internal static async ValueTask SendAsync(IConnection connection, byte[] bytes, CancellationToken cancellationToken)
    {
        try
        {
            await connection.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            throw WsIoFailures.SendFailed(exception);
        }
    }

    private static async ValueTask WriteHeadAsync(Stream? headerOutput, byte[] head, CancellationToken cancellationToken)
    {
        if (headerOutput is null)
        {
            return;
        }

        try
        {
            await headerOutput.WriteAsync(head, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            throw WsIoFailures.HeaderWriteFailed(head.Length, exception);
        }
    }

    private string NewKey()
    {
        Span<byte> key = stackalloc byte[KeyLength];
        randomSource.Fill(key);
        return Convert.ToBase64String(key);
    }
}
