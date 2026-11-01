namespace Curl.Extended.Protocol.OciRegistry.Http;

/// <summary>Why a request to a registry or its token endpoint failed before a response was read.</summary>
public enum OciHttpFailure
{
    /// <summary>The URL is not an absolute <c>http</c> or <c>https</c> URL.</summary>
    UnsupportedEndpoint,

    /// <summary>The connector could not open a connection to the URL's host.</summary>
    ConnectFailed,

    /// <summary>The request holds a field value with a CR or LF, which would inject a field.</summary>
    InvalidRequestField,

    /// <summary>Reading from or writing to the connection failed.</summary>
    ConnectionFailed,

    /// <summary>The server closed the connection before sending any byte of a status line.</summary>
    NoResponse,

    /// <summary>The status line is not <c>HTTP/1.x NNN reason</c> (RFC 9112 §4).</summary>
    MalformedStatusLine,

    /// <summary>A header field line has no name, whitespace before its colon, or is an obsolete fold (RFC 9112 §5).</summary>
    MalformedHeaderField,

    /// <summary>The <c>Content-Length</c> is not a non-negative decimal number (RFC 9112 §6.3).</summary>
    InvalidContentLength,

    /// <summary>A chunk size is not hexadecimal, or a chunk's data is not followed by CRLF (RFC 9112 §7.1).</summary>
    InvalidChunk,

    /// <summary>A line of the response head is longer than <see cref="OciHttpConnectionReader.MaxLineLength"/> bytes.</summary>
    LineTooLong,

    /// <summary>The response body is longer than <see cref="OciHttpBodyReader.MaxBodyLength"/> bytes.</summary>
    BodyTooLarge,

    /// <summary>The server closed the connection after the status line, before the head or its framed body ended.</summary>
    ClosedEarly,
}
