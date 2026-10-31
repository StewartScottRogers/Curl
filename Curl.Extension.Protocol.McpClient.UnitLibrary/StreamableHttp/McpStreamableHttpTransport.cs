using System.Text;
using Curl.Extension.Protocol.McpClient.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.McpClient.StreamableHttp;

/// <summary>
/// Carries MCP messages to one MCP endpoint over the Streamable HTTP transport (Model Context Protocol 2025-06-18,
/// "Base Protocol &gt; Transports &gt; Streamable HTTP"), as HTTP/1.1 requests (RFC 9112) on connections
/// <c>openConnection</c> gives it, one exchange at a time.
/// </summary>
/// <remarks>
/// Each message is a POST with <c>Accept: application/json, text/event-stream</c>. The <c>Mcp-Session-Id</c> a response
/// assigns is sent on every later request, and <see cref="ProtocolVersion"/>, once the caller sets it after initialization,
/// as <c>MCP-Protocol-Version</c>. A connection is kept for the next exchange only when its response was read to its end and
/// leaves it open; otherwise it is disposed and the next exchange opens another. Every failure of an exchange is an
/// <see cref="McpHttpException"/>, or the <see cref="McpWireFormatException"/> of a message that is not JSON-RPC.
/// </remarks>
public sealed class McpStreamableHttpTransport : IAsyncDisposable
{
    private readonly Func<CancellationToken, ValueTask<IConnection>> openConnection;
    private readonly string authority;
    private readonly string target;
    private IConnection? connection;
    private McpHttpConnectionReader? reader;
    private string? protocolVersion;

    /// <summary>Initializes a new instance of the <see cref="McpStreamableHttpTransport"/> class.</summary>
    /// <param name="openConnection">Opens a connection to the endpoint's host, over TLS for an https endpoint.</param>
    /// <param name="authority">The <c>Host</c> header's value, such as <c>example.com:8080</c>.</param>
    /// <param name="target">The MCP endpoint's request target in origin form, such as <c>/mcp</c>, already percent-encoded.</param>
    public McpStreamableHttpTransport(Func<CancellationToken, ValueTask<IConnection>> openConnection, string authority, string target)
    {
        ArgumentNullException.ThrowIfNull(openConnection);
        this.openConnection = openConnection;
        this.authority = RequireVisibleAscii(authority, nameof(authority));
        this.target = RequireVisibleAscii(target, nameof(target));
    }

    /// <summary>Gets the session id the server assigned with <c>Mcp-Session-Id</c> ("Session Management"), or <see langword="null"/>.</summary>
    public string? SessionId { get; private set; }

    /// <summary>
    /// Gets or sets the protocol version sent as <c>MCP-Protocol-Version</c> on every request ("Protocol Version Header"), or
    /// <see langword="null"/> to send none, as before initialization.
    /// </summary>
    /// <exception cref="ArgumentException">The value holds a character that is not visible ASCII.</exception>
    public string? ProtocolVersion
    {
        get => protocolVersion;
        set => protocolVersion = value is null ? null : RequireVisibleAscii(value, nameof(value));
    }

    /// <summary>
    /// Sends a JSON-RPC request as a POST and returns its response from the <c>application/json</c> or <c>text/event-stream</c>
    /// reply ("Sending Messages to the Server"), skipping the server's own requests and notifications in an event stream.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The response whose id is the request's.</returns>
    /// <exception cref="McpHttpException">The status is not 200, the reply's type is neither, or the reply ended without the response.</exception>
    public async Task<McpResponse> SendRequestAsync(McpOutgoingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await CloseConnectionUnlessKeptAsync(ExchangeRequestAsync(request, cancellationToken));
    }

    /// <summary>
    /// Sends a JSON-RPC notification, or a response to a server's request, as a POST the server must answer with
    /// 202 Accepted ("Sending Messages to the Server").
    /// </summary>
    /// <param name="message">The message as one UTF-8 JSON object.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>A task that completes once the server accepted the message.</returns>
    /// <exception cref="McpHttpException">The status is not 202.</exception>
    public async Task SendNotificationAsync(byte[] message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        await CloseConnectionUnlessKeptAsync(ExchangeNotificationAsync(message, cancellationToken));
    }

    /// <summary>
    /// Ends the session with a DELETE carrying its <c>Mcp-Session-Id</c> ("Session Management"); does nothing without a
    /// session. A 405 Method Not Allowed, the server declining to let the client end it, counts as ended too.
    /// </summary>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>A task that completes once the session is ended and the connection closed.</returns>
    /// <exception cref="McpHttpException">The status is neither 2xx nor 405.</exception>
    public async Task EndSessionAsync(CancellationToken cancellationToken)
    {
        if (SessionId is null)
        {
            return;
        }

        await CloseConnectionUnlessKeptAsync(ExchangeEndSessionAsync(cancellationToken));
        SessionId = null;
    }

    /// <summary>Disposes the open connection, if any.</summary>
    /// <returns>A task that completes once it is disposed.</returns>
    public async ValueTask DisposeAsync() => await CloseConnectionAsync();

    private static string RequireVisibleAscii(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        return IsVisibleAscii(value)
            ? value
            : throw new ArgumentException("The value is empty or holds a character that is not visible ASCII.", parameterName);
    }

    private static bool IsVisibleAscii(string value) =>
        value.Length > 0 && !value.AsSpan().ContainsAnyExceptInRange('\x21', '\x7E');

    private static void RequireStatus(McpHttpResponse response, int statusCode)
    {
        if (response.StatusCode != statusCode)
        {
            throw UnexpectedStatus(response.StatusCode);
        }
    }

    private static McpHttpException UnexpectedStatus(int statusCode) =>
        new(McpHttpFailure.UnexpectedStatus, $"The server answered with status {statusCode}.", statusCode);

    private static McpHttpException NoResponse() =>
        new(McpHttpFailure.NoResponse, "The reply ended without the response to the request.");

    private static async Task<McpResponse> ReadEventStreamResponseAsync(McpEventStreamReader events, long awaitedId, CancellationToken cancellationToken)
    {
        for (var data = await events.ReadEventDataAsync(cancellationToken); data is not null; data = await events.ReadEventDataAsync(cancellationToken))
        {
            if (McpResponseReader.ReadResponse(data, awaitedId) is { } response)
            {
                return response;
            }
        }

        throw NoResponse();
    }

    private void RecordSessionId(McpHttpResponse response)
    {
        var sessionId = response.FindHeader("Mcp-Session-Id");
        if (sessionId is null)
        {
            return;
        }

        SessionId = IsVisibleAscii(sessionId)
            ? sessionId
            : throw new McpHttpException(McpHttpFailure.MalformedHeaderField, "The Mcp-Session-Id is empty or holds a character that is not visible ASCII.");
    }

    private async Task<McpHttpResponse> ExchangeAsync(string method, byte[]? body, CancellationToken cancellationToken)
    {
        connection ??= await openConnection(cancellationToken);
        reader ??= new McpHttpConnectionReader(connection);
        await connection.WriteAsync(Encoding.ASCII.GetBytes(FormatHead(method, body?.Length)), cancellationToken);
        if (body is not null)
        {
            await connection.WriteAsync(body, cancellationToken);
        }

        await connection.FlushAsync(cancellationToken);
        return await McpHttpResponseHeadReader.ReadAsync(reader, cancellationToken);
    }

    private string FormatHead(string method, int? bodyLength)
    {
        var head = new StringBuilder().Append(method).Append(' ').Append(target).Append(" HTTP/1.1\r\nHost: ").Append(authority).Append("\r\n");
        if (bodyLength is { } length)
        {
            head.Append("Accept: application/json, text/event-stream\r\nContent-Type: application/json\r\nContent-Length: ").Append(length).Append("\r\n");
        }

        AppendField(head, "Mcp-Session-Id", SessionId);
        AppendField(head, "MCP-Protocol-Version", protocolVersion);
        return head.Append("\r\n").ToString();
    }

    private static void AppendField(StringBuilder head, string name, string? value)
    {
        if (value is not null)
        {
            head.Append(name).Append(": ").Append(value).Append("\r\n");
        }
    }

    private async Task<(McpResponse Value, bool KeepConnection)> ExchangeRequestAsync(McpOutgoingRequest request, CancellationToken cancellationToken)
    {
        var response = await ExchangeAsync("POST", request.Bytes, cancellationToken);
        RequireStatus(response, 200);
        RecordSessionId(response);
        var mediaType = (response.FindHeader("Content-Type") ?? string.Empty).Split(';')[0].Trim();
        if (mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase))
        {
            var answer = McpResponseReader.ReadResponse(await response.Body.ReadToEndAsync(cancellationToken), request.Id) ?? throw NoResponse();
            return (answer, response.ConnectionPersists);
        }

        return mediaType.Equals("text/event-stream", StringComparison.OrdinalIgnoreCase)
            ? (await ReadEventStreamResponseAsync(new McpEventStreamReader(response.Body), request.Id, cancellationToken), false)
            : throw new McpHttpException(McpHttpFailure.UnexpectedContentType, $"The reply's Content-Type \"{mediaType}\" is neither application/json nor text/event-stream.");
    }

    private async Task<(bool Value, bool KeepConnection)> ExchangeNotificationAsync(byte[] message, CancellationToken cancellationToken)
    {
        var response = await ExchangeAsync("POST", message, cancellationToken);
        RequireStatus(response, 202);
        await response.Body.ReadToEndAsync(cancellationToken);
        return (true, response.ConnectionPersists);
    }

    private async Task<(bool Value, bool KeepConnection)> ExchangeEndSessionAsync(CancellationToken cancellationToken)
    {
        var response = await ExchangeAsync("DELETE", null, cancellationToken);
        if (response.StatusCode != 405 && response.StatusCode / 100 != 2)
        {
            throw UnexpectedStatus(response.StatusCode);
        }

        return (true, false);
    }

    /// <summary>
    /// Awaits <paramref name="exchange"/> and closes the connection unless the exchange succeeded and kept it, without a
    /// try block, then returns the exchange's value or throws its exception.
    /// </summary>
    private async Task<T> CloseConnectionUnlessKeptAsync<T>(Task<(T Value, bool KeepConnection)> exchange)
    {
        await ((Task)exchange).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (!exchange.IsCompletedSuccessfully || !(await exchange).KeepConnection)
        {
            await CloseConnectionAsync();
        }

        return (await exchange).Value;
    }

    private async ValueTask CloseConnectionAsync()
    {
        if (connection is null)
        {
            return;
        }

        var closing = connection;
        connection = null;
        reader = null;
        await closing.DisposeAsync();
    }
}
