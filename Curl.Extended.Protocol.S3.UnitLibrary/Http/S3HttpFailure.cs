namespace Curl.Extended.Protocol.S3.Http;

/// <summary>Why an S3 HTTP/1.1 exchange failed.</summary>
public enum S3HttpFailure
{
    /// <summary>The status line is not <c>HTTP/1.x NNN reason</c> (RFC 9112 §4).</summary>
    MalformedStatusLine,

    /// <summary>A header field line has no name, whitespace before its colon, or is an obsolete fold (RFC 9112 §5).</summary>
    MalformedHeaderField,

    /// <summary>The <c>Content-Length</c> is not a non-negative decimal number (RFC 9112 §6.3).</summary>
    InvalidContentLength,

    /// <summary>A chunk size is not hexadecimal, or a chunk's data is not followed by CRLF (RFC 9112 §7.1).</summary>
    InvalidChunk,

    /// <summary>A line of the response head is longer than <see cref="S3HttpConnectionReader.MaxLineLength"/> bytes.</summary>
    LineTooLong,

    /// <summary>The server closed the connection before the response head or its framed body ended.</summary>
    ClosedEarly,

    /// <summary>The request body stream ended before the length the request declared.</summary>
    RequestBodyTooShort,
}
