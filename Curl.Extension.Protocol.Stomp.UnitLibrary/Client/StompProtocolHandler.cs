using Curl.Extension.Protocol.Stomp.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.Stomp.Client;

/// <summary>
/// Serves <c>stomp://host[:port]/destination</c> (port 61613) and <c>stomps://</c> (port 61614, TLS from the
/// connector) with STOMP 1.2 (ADR-1059): with a body (<c>-d</c>) it sends it to the URL's destination, otherwise it
/// subscribes to the destination and writes each message body to the output. <c>--user</c> goes into
/// <c>CONNECT</c>'s <c>login</c> and <c>passcode</c>. The exchange itself is <see cref="StompClientFlow"/>.
/// </summary>
/// <param name="connector">Opens the connection to the server.</param>
internal sealed class StompProtocolHandler(IConnector connector) : IProtocolHandler
{
    /// <summary>The port <c>stomp://</c> connects to when the URL names none.</summary>
    internal const int DefaultPort = 61613;

    /// <summary>The port <c>stomps://</c> connects to when the URL names none.</summary>
    internal const int SecureDefaultPort = 61614;

    private const string SecureScheme = "stomps";

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes { get; } = ["stomp", SecureScheme];

    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        string path = context.Url.AbsolutePath;
        if (path.Length <= 1)
        {
            return TransferResult.Failure(CurlExitCode.UrlMalformat, StompClientMessages.NoDestination);
        }

        return await TransferAsync(context, Uri.UnescapeDataString(path)).ConfigureAwait(false);
    }

    /// <summary>Gives where to connect: the URL's host, its port or the scheme's default, and TLS for <c>stomps</c>.</summary>
    internal static ConnectTarget TargetOf(ITransferContext context)
    {
        CurlUrl url = context.Url;
        bool useTls = string.Equals(url.Scheme, SecureScheme, StringComparison.OrdinalIgnoreCase);
        int port = url.Port >= 0 ? url.Port : useTls ? SecureDefaultPort : DefaultPort;
        return new ConnectTarget(url.IdnHost, port, useTls)
        {
            Proxy = context.Proxy,
            Events = context.Events,
            DiagnosticLog = context.DiagnosticLog,
        };
    }

    /// <summary>
    /// Gives the <c>CONNECT</c> frame (STOMP 1.2, "CONNECT or STOMP Frame"): version 1.2 only, the URL's host as the
    /// virtual host, no heart-beating, and <c>login</c> and <c>passcode</c> from <paramref name="credentials"/>.
    /// </summary>
    internal static StompFrame ConnectFor(string host, System.Net.NetworkCredential? credentials)
    {
        List<StompHeader> headers = [new("accept-version", "1.2"), new("host", host), new("heart-beat", "0,0")];
        if (credentials is not null)
        {
            headers.Add(new("login", credentials.UserName));
            headers.Add(new("passcode", credentials.Password));
        }

        return new StompFrame("CONNECT", headers, []);
    }

    private async ValueTask<TransferResult> TransferAsync(ITransferContext context, string destination)
    {
        ConnectResult connect = await connector.ConnectAsync(TargetOf(context), context.CancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return new TransferResult(connect.ExitCode, 0, connect.ErrorMessage) { IsConnectionRefused = connect.IsConnectionRefused };
        }

        context.Progress.ReportTransferStarted();
        await using (connection.ConfigureAwait(false))
        {
            var flow = new StompClientFlow(connection, context.Output, context.Progress, context.CancellationToken);
            try
            {
                await flow.RunAsync(ConnectFor(context.Url.Host, context.Credentials), destination, context.PostData).ConfigureAwait(false);
                return TransferResult.Success(flow.BytesWritten);
            }
            catch (StompClientException failure)
            {
                return new TransferResult(failure.ExitCode, flow.BytesWritten, failure.Message);
            }
        }
    }
}
