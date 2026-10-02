namespace Curl.Tls;

/// <summary>The certificate chain a server presented, as <see cref="IServerCertificateVerifier" /> receives it.</summary>
/// <param name="Certificates">The DER certificates in the order the server sent them, leaf first.</param>
/// <param name="HostName">The name the client offered in <c>server_name</c>, or <see langword="null" /> when it offered none.</param>
/// <param name="OcspResponse">The OCSP response stapled to the leaf, or <see langword="null" /> when none was.</param>
public sealed record ServerCertificateChain(IReadOnlyList<byte[]> Certificates, string? HostName, byte[]? OcspResponse)
{
    /// <summary>
    /// Gets the version the server selected, as its wire value (<c>0x0304</c> for TLS 1.3), or
    /// zero when not given; a handshake whose verifier refuses the chain keeps no other record
    /// of it (BL-1178).
    /// </summary>
    public ushort ProtocolVersion { get; init; }

    /// <summary>Gets the code point of the suite the server selected, or zero when not given.</summary>
    public ushort CipherSuite { get; init; }

    /// <summary>Gets the protocol the server selected through ALPN, or <see langword="null" /> when it selected none.</summary>
    public string? ApplicationProtocol { get; init; }
}
