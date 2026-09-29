namespace Curl.Kerberos;

/// <summary>
/// Reaches a KDC through an MS-KKDCP proxy, an <c>https://</c> <c>kdc</c> entry: one HTTPS
/// POST of a <c>KDC-PROXY-MESSAGE</c> and the body it answers with. <see cref="KerberosKdcSender" />
/// wraps and unwraps the message itself (<see cref="KerberosKdcProxyMessage" />), so an
/// implementation only moves bytes: it verifies the proxy's certificate, sends
/// <c>POST /path</c> with <c>Content-Type: application/kerberos</c>, as MIT's
/// <c>sendto_kdc.c</c> does, and owns the timeouts.
/// </summary>
public interface IKerberosKdcProxyTransport
{
    /// <summary>Posts <paramref name="body" /> to the proxy over HTTPS and returns the body of its <c>200</c> reply.</summary>
    /// <param name="host">The proxy's host name or address, without IPv6 brackets.</param>
    /// <param name="port">The proxy's port.</param>
    /// <param name="path">The proxy's path without its leading slash; empty for <c>/</c>.</param>
    /// <param name="body">The encoded <c>KDC-PROXY-MESSAGE</c>.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The reply body.</returns>
    /// <exception cref="IOException">The proxy cannot be reached, fails TLS, answers with another status, or does not answer in time.</exception>
    Task<byte[]> PostAsync(string host, int port, string path, ReadOnlyMemory<byte> body, CancellationToken cancellationToken);
}
