using Curl.Extension.Protocol.McpClient.StreamableHttp;
using Curl.Extension.Protocol.McpClient.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.McpClient.Client;

/// <summary>
/// An MCP session over Streamable HTTP to the endpoint an <c>mcp://</c> (port 80) or <c>mcps://</c> (port 443, TLS) URL names,
/// on connections curl's connector opens, so proxies and connection events work as for curl's own schemes.
/// </summary>
internal sealed class McpHttpSession : IMcpSession
{
    /// <summary>The port <c>mcp://</c> connects to when the URL names none.</summary>
    internal const int DefaultPort = 80;

    /// <summary>The port <c>mcps://</c> connects to when the URL names none.</summary>
    internal const int SecureDefaultPort = 443;

    /// <summary>The scheme that adds TLS.</summary>
    internal const string SecureScheme = "mcps";

    private readonly McpStreamableHttpTransport transport;

    /// <summary>Creates the session for the URL of <paramref name="context"/>; it connects at its first message.</summary>
    /// <param name="connector">Opens the connection.</param>
    /// <param name="context">The transfer, for its URL, proxy and connection events.</param>
    public McpHttpSession(IConnector connector, ITransferContext context)
    {
        ConnectTarget target = TargetOf(context);
        CurlUrl url = context.Url;
        string authority = url.Port >= 0 ? $"{url.IdnHost}:{url.Port}" : url.IdnHost;
        string requestTarget = url.Query is null ? url.AbsolutePath : $"{url.AbsolutePath}?{url.Query}";
        transport = new McpStreamableHttpTransport(
            cancellationToken => OpenAsync(connector, target, cancellationToken),
            authority,
            requestTarget);
    }

    /// <summary>Gives where to connect: the URL's host, its port or the scheme's default, and TLS for <c>mcps</c>.</summary>
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

    /// <inheritdoc />
    public Task<McpResponse> SendRequestAsync(McpOutgoingRequest request, CancellationToken cancellationToken) =>
        transport.SendRequestAsync(request, cancellationToken);

    /// <inheritdoc />
    public Task SendNotificationAsync(byte[] message, CancellationToken cancellationToken) =>
        transport.SendNotificationAsync(message, cancellationToken);

    /// <summary>Sends <paramref name="protocolVersion"/> as <c>MCP-Protocol-Version</c> on every later request.</summary>
    public void UseProtocolVersion(string protocolVersion) => transport.ProtocolVersion = protocolVersion;

    /// <summary>Ends the session with a DELETE carrying its <c>Mcp-Session-Id</c>, when the server assigned one.</summary>
    public Task EndAsync(CancellationToken cancellationToken) => transport.EndSessionAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => transport.DisposeAsync();

    private static async ValueTask<IConnection> OpenAsync(IConnector connector, ConnectTarget target, CancellationToken cancellationToken)
    {
        ConnectResult connect = await connector.ConnectAsync(target, cancellationToken).ConfigureAwait(false);
        return connect.Connection ?? throw new McpClientException(connect.ExitCode, connect.ErrorMessage);
    }
}
