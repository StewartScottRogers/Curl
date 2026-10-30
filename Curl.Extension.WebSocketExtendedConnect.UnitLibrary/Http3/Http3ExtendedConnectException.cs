namespace Curl.Extension.WebSocketExtendedConnect.Http3;

/// <summary>
/// A WebSocket over HTTP/3 (RFC 9220) that could not be opened or that failed while open: a server that does not
/// enable extended CONNECT, a refused CONNECT, a reset stream or a WebSocket frame the server got wrong.
/// </summary>
internal sealed class Http3ExtendedConnectException : IOException
{
    /// <summary>Creates the failure.</summary>
    /// <param name="message">What failed, citing the RFC section.</param>
    /// <param name="innerException">The failure underneath, or <see langword="null" />.</param>
    public Http3ExtendedConnectException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
