using System.Xml.Linq;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Xmpp.Client;

/// <summary>
/// Serves <c>xmpp://[user@]domain[:port]/[target-jid][?query]</c> (port 5222, STARTTLS when offered) and <c>xmpps://</c>
/// (port 5223, TLS from the connector, XEP-0368) as RFC 5122's authority form (ADR-1068): it logs in to the URL's domain
/// with <c>--user</c>, then sends the message or disco query <see cref="XmppRequests"/> makes of the URL's query to the
/// path's JID, or to the domain itself when the path is empty. The exchange is <see cref="XmppClientFlow"/>.
/// </summary>
/// <param name="connector">Opens the connection to the server.</param>
/// <param name="tlsProvider">Secures the connection after STARTTLS.</param>
/// <param name="createClientNonce">Gives a fresh SCRAM client nonce.</param>
internal sealed class XmppProtocolHandler(IConnector connector, ITlsProvider tlsProvider, Func<string> createClientNonce) : IProtocolHandler
{
    /// <summary>Creates the handler with random SCRAM client nonces.</summary>
    /// <param name="connector">Opens the connection to the server.</param>
    /// <param name="tlsProvider">Secures the connection after STARTTLS.</param>
    internal XmppProtocolHandler(IConnector connector, ITlsProvider tlsProvider)
        : this(connector, tlsProvider, XmppScramExchange.CreateClientNonce)
    {
    }

    /// <summary>The port <c>xmpp://</c> connects to when the URL names none (RFC 6120, 3.2.1).</summary>
    internal const int DefaultPort = 5222;

    /// <summary>The port <c>xmpps://</c> connects to when the URL names none (XEP-0368).</summary>
    internal const int SecureDefaultPort = 5223;

    private const string SecureScheme = "xmpps";

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes { get; } = ["xmpp", SecureScheme];

    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        string path = Uri.UnescapeDataString(context.Url.AbsolutePath.TrimStart('/'));
        XElement request;
        try
        {
            request = XmppRequests.For(path.Length > 0 ? path : context.Url.Host, context.Url.Query, context.PostData);
        }
        catch (XmppClientException failure)
        {
            return TransferResult.Failure(failure.ExitCode, failure.Message);
        }

        return await TransferAsync(context, request).ConfigureAwait(false);
    }

    /// <summary>Gives where to connect: the URL's host, its port or the scheme's default, and TLS for <c>xmpps</c>.</summary>
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

    private async ValueTask<TransferResult> TransferAsync(ITransferContext context, XElement request)
    {
        ConnectResult connect = await connector.ConnectAsync(TargetOf(context), context.CancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return new TransferResult(connect.ExitCode, 0, connect.ErrorMessage) { IsConnectionRefused = connect.IsConnectionRefused };
        }

        context.Progress.ReportTransferStarted();
        await using (connection.ConfigureAwait(false))
        {
            var flow = new XmppClientFlow(connection, tlsProvider, context.Url.Host, context.Output, context.Progress, createClientNonce, context.CancellationToken);
            TransferResult result = await RunFlowAsync(flow, context, request).ConfigureAwait(false);
            await flow.DisposeAsync().ConfigureAwait(false);
            return result;
        }
    }

    private static async ValueTask<TransferResult> RunFlowAsync(XmppClientFlow flow, ITransferContext context, XElement request)
    {
        try
        {
            await flow.RunAsync(context.Credentials, request).ConfigureAwait(false);
            return TransferResult.Success(flow.BytesWritten);
        }
        catch (XmppClientException failure)
        {
            return new TransferResult(failure.ExitCode, flow.BytesWritten, failure.Message);
        }
    }
}
