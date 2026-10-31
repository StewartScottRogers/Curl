namespace Curl.Extension.WebSocketExtendedConnect.Http2;

/// <summary>
/// A WebSocket over HTTP/2 could not be opened or run: the server did not advertise
/// SETTINGS_ENABLE_CONNECT_PROTOCOL, refused the extended CONNECT, reset the stream or closed the connection
/// (RFC 8441).
/// </summary>
internal sealed class Http2WebSocketException : Exception
{
    /// <summary>Creates the exception with the reason the WebSocket failed.</summary>
    /// <param name="message">The reason, naming the RFC 8441 section that applies.</param>
    internal Http2WebSocketException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with the reason the WebSocket failed and the HTTP/2 failure behind it.</summary>
    /// <param name="message">The reason, naming the RFC 8441 section that applies.</param>
    /// <param name="innerException">The HTTP/2 failure.</param>
    internal Http2WebSocketException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
