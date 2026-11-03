namespace Curl.Extended.Protocol.McpClient.StreamableHttp;

/// <summary>Why an MCP Streamable HTTP exchange failed.</summary>
public enum McpHttpFailure
{
    /// <summary>The status line is not <c>HTTP/1.x NNN reason</c> (RFC 9112 §4).</summary>
    MalformedStatusLine,

    /// <summary>A header field line has no name, whitespace before its colon, or is an obsolete fold (RFC 9112 §5), or the <c>Mcp-Session-Id</c> holds a character that is not visible ASCII.</summary>
    MalformedHeaderField,

    /// <summary>The <c>Content-Length</c> is not a non-negative decimal number (RFC 9112 §6.3).</summary>
    InvalidContentLength,

    /// <summary>A chunk size is not hexadecimal, or a chunk's data is not followed by CRLF (RFC 9112 §7.1).</summary>
    InvalidChunk,

    /// <summary>A line of the response head is longer than <see cref="McpHttpConnectionReader.MaxLineLength"/> bytes.</summary>
    LineTooLong,

    /// <summary>The server closed the connection before the response head or its framed body ended.</summary>
    ClosedEarly,

    /// <summary>The server answered with a status the transport does not accept for the message; <see cref="McpHttpException.StatusCode"/> holds it.</summary>
    UnexpectedStatus,

    /// <summary>
    /// The server answered a request carrying an <c>Mcp-Session-Id</c> with 404 Not Found: the session expired ("Session
    /// Management"), so the client must start a new one with a new <c>initialize</c> carrying no session id.
    /// </summary>
    SessionExpired,

    /// <summary>A reply to a request is neither <c>application/json</c> nor <c>text/event-stream</c>.</summary>
    UnexpectedContentType,

    /// <summary>The reply ended without the JSON-RPC response to the request.</summary>
    NoResponse,
}
