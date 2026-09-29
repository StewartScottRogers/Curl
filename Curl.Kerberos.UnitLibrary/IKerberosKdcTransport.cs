namespace Curl.Kerberos;

/// <summary>
/// Reaches a KDC over the network: one UDP datagram out and one back, or a TCP connection.
/// <see cref="KerberosKdcClient" /> decides which, and frames TCP messages itself (RFC 4120
/// section 7.2.2), so an implementation only moves bytes. The socket implementation lives in
/// <c>Curl.Networking.UnitLibrary</c>; it owns the timeouts.
/// </summary>
public interface IKerberosKdcTransport
{
    /// <summary>Sends <paramref name="request" /> to the KDC in one UDP datagram and returns the datagram it answers with.</summary>
    /// <param name="host">The KDC's host name or address.</param>
    /// <param name="port">The KDC's port.</param>
    /// <param name="request">The whole message.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The reply datagram.</returns>
    /// <exception cref="IOException">The KDC cannot be reached or does not answer in time.</exception>
    Task<byte[]> ExchangeDatagramAsync(string host, int port, ReadOnlyMemory<byte> request, CancellationToken cancellationToken);

    /// <summary>Opens a TCP connection to the KDC.</summary>
    /// <param name="host">The KDC's host name or address.</param>
    /// <param name="port">The KDC's port.</param>
    /// <param name="cancellationToken">Cancels the connection.</param>
    /// <returns>The connection's stream; the caller disposes it.</returns>
    /// <exception cref="IOException">The KDC cannot be reached.</exception>
    Task<Stream> ConnectStreamAsync(string host, int port, CancellationToken cancellationToken);
}
