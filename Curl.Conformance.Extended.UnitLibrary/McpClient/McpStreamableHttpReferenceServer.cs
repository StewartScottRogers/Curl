using System.Globalization;
using System.Text;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.McpClient;

/// <summary>
/// A reference MCP server on the Streamable HTTP transport (Model Context Protocol 2025-06-18, "Base Protocol &gt;
/// Transports &gt; Streamable HTTP"), serving <paramref name="toolServer" />'s answers: it assigns an
/// <c>Mcp-Session-Id</c> with the <c>initialize</c> response, answers a request carrying no session id with 400 and one
/// carrying an unknown or expired id with 404, a notification with 202, a request with <c>application/json</c> or an
/// event stream, and a DELETE by ending the session. It records every request, across connections.
/// </summary>
/// <param name="toolServer">The JSON-RPC side.</param>
public sealed class McpStreamableHttpReferenceServer(McpReferenceToolServer toolServer) : IReferenceServer
{
    private readonly Lock gate = new();

    private readonly List<McpHttpRequestRecord> requests = [];

    private int sessionsStarted;

    private string? sessionId;

    private bool sessionExpired;

    /// <summary>Gets a value indicating whether a request is answered with a <c>text/event-stream</c> that sends two unrelated notifications before the response.</summary>
    public bool RepliesWithEventStream { get; init; }

    /// <summary>Gets the JSON-RPC method whose first request ends the first session, answered with 404; <see langword="null" /> keeps it.</summary>
    public string? ExpiresSessionAtMethod { get; init; }

    /// <summary>
    /// Gets the <c>Authorization</c> value every request must carry ("Authorization": a request without it, or with another
    /// value, is answered 401 Unauthorized); <see langword="null" /> requires none.
    /// </summary>
    public string? RequiredAuthorization { get; init; }

    /// <summary>Gets every request received so far, in order.</summary>
    public IReadOnlyList<McpHttpRequestRecord> Requests
    {
        get
        {
            lock (gate)
            {
                return [.. requests];
            }
        }
    }

    /// <inheritdoc />
    public async Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ReferenceHttpRequestReader reader = new(connection);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false) is { } request)
        {
            string reply;
            lock (gate)
            {
                requests.Add(request);
                reply = Reply(request);
            }

            await connection.WriteAsync(Encoding.UTF8.GetBytes(reply), cancellationToken).ConfigureAwait(false);
        }
    }

    private string Reply(McpHttpRequestRecord request) =>
        RequiredAuthorization is not null && request.Header("Authorization") != RequiredAuthorization ? Response("401 Unauthorized", null, null, string.Empty)
        : request.Method == "DELETE" ? EndSession()
        : request.JsonRpcMethod == "initialize" ? Answer(request, StartSession())
        : RejectSessionOf(request) ?? Answer(request, null);

    private string EndSession()
    {
        sessionId = null;
        return Response("200 OK", null, null, string.Empty);
    }

    private string? RejectSessionOf(McpHttpRequestRecord request)
    {
        string? carriedId = request.Header("Mcp-Session-Id");
        if (carriedId is null)
        {
            return Response("400 Bad Request", null, null, string.Empty);
        }

        return carriedId != sessionId || ExpiresHere(request) ? Response("404 Not Found", null, null, string.Empty) : null;
    }

    private string Answer(McpHttpRequestRecord request, string? newSessionId)
    {
        string? answer = toolServer.Answer(request.Body);
        return answer is null ? Response("202 Accepted", null, null, string.Empty) : Reply(answer, newSessionId);
    }

    private bool ExpiresHere(McpHttpRequestRecord request)
    {
        if (sessionExpired || request.JsonRpcMethod != ExpiresSessionAtMethod)
        {
            return false;
        }

        sessionExpired = true;
        sessionId = null;
        return true;
    }

    private string StartSession()
    {
        sessionsStarted++;
        sessionId = "session-" + sessionsStarted.ToString(CultureInfo.InvariantCulture);
        return sessionId;
    }

    private string Reply(string message, string? newSessionId) =>
        RepliesWithEventStream
            ? Response(
                "200 OK",
                "text/event-stream",
                newSessionId,
                ": unrelated messages first\n\n"
                + "event: message\ndata: {\"jsonrpc\":\"2.0\",\"method\":\"notifications/message\",\"params\":{\"level\":\"info\",\"data\":\"working\"}}\n\n"
                + "data: {\"jsonrpc\":\"2.0\",\"method\":\"notifications/progress\",\n"
                + "data: \"params\":{\"progressToken\":\"p\",\"progress\":1}}\n\n"
                + $"id: 1\nevent: message\ndata: {message}\n\n")
            : Response("200 OK", "application/json", newSessionId, message);

    private static string Response(string status, string? contentType, string? newSessionId, string body)
    {
        StringBuilder head = new StringBuilder("HTTP/1.1 ").Append(status).Append("\r\n");
        if (contentType is not null)
        {
            head.Append("Content-Type: ").Append(contentType).Append("\r\n");
        }

        if (newSessionId is not null)
        {
            head.Append("Mcp-Session-Id: ").Append(newSessionId).Append("\r\n");
        }

        int length = Encoding.UTF8.GetByteCount(body);
        return head.Append("Content-Length: ").Append(length.ToString(CultureInfo.InvariantCulture)).Append("\r\n\r\n").Append(body).ToString();
    }
}
