using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.SignalR.Transport;

/// <summary>
/// Thrown when a SignalR server's HTTP or WebSocket exchange fails: a negotiate status that is not 2xx, a negotiate
/// error or a second redirect, an upgrade the server did not accept, a broken HTTP response or WebSocket frame, or a
/// connection that ended early (TransportProtocols.md, RFC 6455).
/// </summary>
internal sealed class SignalRTransportException : IOException
{
    /// <summary>Creates the exception with the message naming what failed and the exit code the transfer reports.</summary>
    public SignalRTransportException(string message, CurlExitCode exitCode = CurlExitCode.WeirdServerReply)
        : base(message)
    {
        ExitCode = exitCode;
    }

    /// <summary>Gets the exit code the transfer reports: 8 unless the server refused the request with HTTP 401 or 403, then 22.</summary>
    public CurlExitCode ExitCode { get; }
}
