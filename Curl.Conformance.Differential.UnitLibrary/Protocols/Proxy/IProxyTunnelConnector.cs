namespace Curl.Conformance.Differential.Protocols.Proxy;

/// <summary>Opens the outbound connection a CONNECT tunnel runs over (a <c>TcpClient</c> in the host, a stream pair in unit tests).</summary>
public interface IProxyTunnelConnector
{
    /// <summary>Connects to <paramref name="host"/> and <paramref name="port"/>; <see langword="null"/> means the target could not be reached.</summary>
    Task<Stream?> ConnectAsync(string host, int port, CancellationToken cancellationToken);
}
