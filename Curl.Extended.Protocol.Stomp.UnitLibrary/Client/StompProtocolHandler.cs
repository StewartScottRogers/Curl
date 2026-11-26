using Curl.Extended.Protocol.Stomp.Wire;
using Curl.Extension.Abstractions;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Stomp.Client;

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

    /// <summary>The virtual host <c>CONNECT</c> names when no <c>-H "host: ..."</c> gives one (ADR-1285): <c>/</c>, the default of stock brokers.</summary>
    internal const string DefaultVirtualHost = "/";

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

        string? field = LineBreakField(VirtualHostOf(context.Http?.Headers), context.Credentials);
        if (field is not null)
        {
            return TransferResult.Failure(CurlExitCode.UrlMalformat, $"STOMP {field} holds a line break");
        }

        return UrlPercentDecoding.Utf8TextOf(path) is { } destination
            ? await TransferAsync(context, destination).ConfigureAwait(false)
            : TransferResult.Failure(CurlExitCode.UrlMalformat, StompClientMessages.DestinationNotUtf8);
    }

    /// <summary>
    /// Gives the name of the first <c>CONNECT</c> header value holding a line feed or carriage return (STOMP 1.2 does not
    /// escape <c>CONNECT</c> headers, so such a value would add headers to the frame), or null when none does.
    /// </summary>
    internal static string? LineBreakField(string host, System.Net.NetworkCredential? credentials)
    {
        if (host.AsSpan().ContainsAny('\r', '\n'))
        {
            return "host";
        }

        if (credentials is null)
        {
            return null;
        }

        if (credentials.UserName.AsSpan().ContainsAny('\r', '\n'))
        {
            return "login";
        }

        return credentials.Password.AsSpan().ContainsAny('\r', '\n') ? "passcode" : null;
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
    /// Gives the <c>CONNECT</c> frame (STOMP 1.2, "CONNECT or STOMP Frame"): version 1.2 only, <paramref name="host"/> as the
    /// virtual host (ADR-1285), no heart-beating, and <c>login</c> and <c>passcode</c> from <paramref name="credentials"/>.
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

    /// <summary>
    /// Gives the <c>content-type</c> for the <c>SEND</c> frame (ADR-1220): the value of the last <c>-H "content-type: ..."</c>
    /// in <paramref name="headers"/> (name matched without regard to case), or <see cref="StompClientFlow.SendContentType"/>
    /// when there is none or its value is empty.
    /// </summary>
    internal static string ContentTypeOf(IReadOnlyList<string>? headers) =>
        LastHeaderValue(headers, "content-type") ?? StompClientFlow.SendContentType;

    /// <summary>
    /// Gives the virtual host for <c>CONNECT</c>'s <c>host</c> (ADR-1285): the value of the last <c>-H "host: ..."</c> in
    /// <paramref name="headers"/> (name matched without regard to case), or <see cref="DefaultVirtualHost"/> when there is
    /// none or its value is empty.
    /// </summary>
    internal static string VirtualHostOf(IReadOnlyList<string>? headers) =>
        LastHeaderValue(headers, "host") ?? DefaultVirtualHost;

    private static string? LastHeaderValue(IReadOnlyList<string>? headers, string name)
    {
        string? value = null;
        foreach (string header in headers ?? [])
        {
            int colon = header.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0 && header.AsSpan(0, colon).Trim().Equals(name, StringComparison.OrdinalIgnoreCase) && header.AsSpan(colon + 1).Trim().Length > 0)
            {
                value = header[(colon + 1)..].Trim();
            }
        }

        return value;
    }

    /// <summary>The SEND body: <c>-d</c>'s data when given, else the <c>-T</c> upload's bytes, else none (the receive path).</summary>
    private static async ValueTask<ReadOnlyMemory<byte>?> RequestBodyAsync(ITransferContext context)
    {
        if (context.PostData is not null || context.Upload is not { } upload)
        {
            return context.PostData;
        }

        using var body = new MemoryStream();
        await upload.CopyToAsync(body, context.CancellationToken).ConfigureAwait(false);
        return body.ToArray();
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
                await flow.RunAsync(ConnectFor(VirtualHostOf(context.Http?.Headers), context.Credentials), destination, await RequestBodyAsync(context).ConfigureAwait(false), ContentTypeOf(context.Http?.Headers)).ConfigureAwait(false);
                return TransferResult.Success(flow.BytesWritten);
            }
            catch (StompClientException failure)
            {
                return new TransferResult(failure.ExitCode, flow.BytesWritten, failure.Message);
            }
        }
    }
}
