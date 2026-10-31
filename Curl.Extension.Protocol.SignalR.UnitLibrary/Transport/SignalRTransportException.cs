namespace Curl.Extension.Protocol.SignalR.Transport;

/// <summary>
/// Thrown when a SignalR server's HTTP or WebSocket exchange fails: a negotiate status that is not 2xx, a negotiate
/// error or a second redirect, an upgrade the server did not accept, a broken HTTP response or WebSocket frame, or a
/// connection that ended early (TransportProtocols.md, RFC 6455).
/// </summary>
internal sealed class SignalRTransportException : IOException
{
    /// <summary>Creates the exception with the message naming what failed.</summary>
    public SignalRTransportException(string message)
        : base(message)
    {
    }
}
