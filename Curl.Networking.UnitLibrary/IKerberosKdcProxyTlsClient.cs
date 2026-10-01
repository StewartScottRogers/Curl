using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// Runs the TLS handshake of a KDC proxy exchange over the plaintext stream, with its own trust
/// rather than the transfer's: <c>-k</c>, <c>--cacert</c> and <c>--capath</c> never apply to a
/// KDC proxy, as they never reach MIT's <c>sendto_kdc.c</c> (ADR-0300).
/// </summary>
internal interface IKerberosKdcProxyTlsClient
{
    /// <summary>Secures <paramref name="plaintext" /> for <paramref name="host" />.</summary>
    /// <param name="plaintext">The connection's stream; ownership transfers to the returned stream, or it is disposed on failure.</param>
    /// <param name="host">The proxy's host, which its certificate must name.</param>
    /// <param name="anchors">The roots the certificate must lead to, or <see langword="null" /> for the system's trust store.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>The secured stream.</returns>
    /// <exception cref="IOException">The handshake fails or the certificate is not trusted.</exception>
    Task<Stream> AuthenticateAsync(Stream plaintext, string host, X509Certificate2Collection? anchors, CancellationToken cancellationToken);
}
