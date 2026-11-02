using System.Security.Cryptography.X509Certificates;

namespace Curl.Conformance.Extended.Harness;

/// <summary>
/// One reference server on one port, reached over TCP, TLS or UDP. Every host name resolves to
/// 127.0.0.1, so a route answers its port whatever host the URL names.
/// </summary>
public sealed record ReferenceRoute
{
    private ReferenceRoute(int port, ReferenceTransport transport)
    {
        Port = port;
        Transport = transport;
    }

    /// <summary>Gets the port the route answers.</summary>
    public int Port { get; }

    /// <summary>Gets how curl's bytes reach the server.</summary>
    public ReferenceTransport Transport { get; }

    /// <summary>Gets the server of a TCP route.</summary>
    public IReferenceServer? StreamServer { get; private init; }

    /// <summary>Gets the certificate a TLS route presents; <see langword="null" /> on a TCP or UDP route.</summary>
    public X509Certificate2? Certificate { get; private init; }

    /// <summary>Gets the server of a UDP route.</summary>
    public IDatagramReferenceServer? DatagramServer { get; private init; }

    /// <summary>A plaintext TCP route.</summary>
    /// <param name="port">The port it answers.</param>
    /// <param name="server">The server each connection reaches.</param>
    /// <returns>The route.</returns>
    public static ReferenceRoute Tcp(int port, IReferenceServer server)
    {
        ArgumentNullException.ThrowIfNull(server);
        return new ReferenceRoute(port, ReferenceTransport.Tcp) { StreamServer = server };
    }

    /// <summary>A TLS route: each connection's TLS handshake is served with <paramref name="certificate" />, no ALPN and no client certificate, and <paramref name="server" /> sees the plaintext.</summary>
    /// <param name="port">The port it answers.</param>
    /// <param name="server">The server each connection's plaintext reaches.</param>
    /// <param name="certificate">The server certificate with its private key, such as <see cref="ReferenceCertificate.CreateSelfSigned" /> makes.</param>
    /// <returns>The route.</returns>
    public static ReferenceRoute Tls(int port, IReferenceServer server, X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(certificate);
        return new ReferenceRoute(port, ReferenceTransport.Tls) { StreamServer = server, Certificate = certificate };
    }

    /// <summary>A UDP route.</summary>
    /// <param name="port">The port it answers.</param>
    /// <param name="server">The server each channel reaches.</param>
    /// <returns>The route.</returns>
    public static ReferenceRoute Udp(int port, IDatagramReferenceServer server)
    {
        ArgumentNullException.ThrowIfNull(server);
        return new ReferenceRoute(port, ReferenceTransport.Udp) { DatagramServer = server };
    }
}
