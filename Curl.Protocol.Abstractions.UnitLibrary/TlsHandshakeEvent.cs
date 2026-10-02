using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// The facts a completed TLS handshake negotiated, reported through
/// <see cref="ITransferEvents.ReportTlsHandshake" /> (ADR-0046).
/// </summary>
/// <remarks>
/// Which of these <c>-v</c> prints depends on the platform's curl build: the Schannel
/// build prints only the ALPN lines, the OpenSSL build also the version, cipher and
/// certificate fields (ADR-0009).
/// </remarks>
public sealed record TlsHandshakeEvent
{
    /// <summary>
    /// Gets the negotiated TLS protocol version.
    /// </summary>
    public required SslProtocols ProtocolVersion { get; init; }

    /// <summary>
    /// Gets the negotiated cipher suite, or <see langword="null" /> when the platform does
    /// not report it.
    /// </summary>
    public required TlsCipherSuite? CipherSuite { get; init; }

    /// <summary>
    /// Gets the application protocol the server accepted through ALPN, or
    /// <see langword="null" /> when none was negotiated.
    /// </summary>
    public required string? NegotiatedApplicationProtocol { get; init; }

    /// <summary>
    /// Gets the application protocols offered through ALPN, in the order offered.
    /// </summary>
    public required IReadOnlyList<string> OfferedApplicationProtocols { get; init; }

    /// <summary>
    /// Gets the server's certificate, or <see langword="null" /> when none was presented.
    /// </summary>
    public required X509Certificate2? ServerCertificate { get; init; }

    /// <summary>
    /// Gets a value indicating whether the server's certificate was verified.
    /// </summary>
    public required bool CertificateVerified { get; init; }

    /// <summary>
    /// Gets OpenSSL's name for the key-exchange group the handshake used, such as
    /// <c>X25519MLKEM768</c> or <c>x25519</c>, or <see langword="null" /> when the platform
    /// does not report it (ADR-0085).
    /// </summary>
    public string? NegotiatedGroupName { get; init; }

    /// <summary>
    /// Gets OpenSSL's short name for the signature type the server signed the handshake
    /// with, such as <c>RSASSA-PSS</c>, or <see langword="null" /> when the platform does
    /// not report it (ADR-0085).
    /// </summary>
    public string? PeerSignatureTypeName { get; init; }

    /// <summary>
    /// Gets the certificate verification result as an OpenSSL <c>X509_V_</c> code, such as
    /// <c>0</c> for verified or <c>18</c> for a self-signed certificate, or
    /// <see langword="null" /> when the platform does not report one (ADR-0085).
    /// </summary>
    public long? CertificateVerifyResult { get; init; }

    /// <summary>
    /// Gets the server's certificate chain, the server's own certificate first: the verified
    /// chain when verification succeeded, else the chain as the server sent it. Empty when
    /// the platform does not report it (ADR-0085).
    /// </summary>
    public IReadOnlyList<X509Certificate2> PeerCertificateChain { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the handshake was with an HTTPS proxy rather than the
    /// origin, which makes curl's OpenSSL build say <c>Proxy certificate:</c> instead of
    /// <c>Server certificate:</c> (ADR-0085).
    /// </summary>
    public bool IsProxy { get; init; }

    /// <summary>
    /// Gets the host name, as the user gave it, that the server's certificate was checked
    /// against, or <see langword="null" /> when the host name was not checked (<c>-k</c>).
    /// An IP address is given without brackets (ADR-0085).
    /// </summary>
    public string? VerifiedHostName { get; init; }

    /// <summary>
    /// Gets a value indicating whether the handshake was a QUIC connect's (HTTP/3), which on
    /// Windows curl.se's LibreSSL build words rather than the Schannel build (ADR-0144).
    /// </summary>
    public bool IsQuic { get; init; }

    /// <summary>
    /// Gets the server key's hash as a <c>sha256//</c> <c>--pinnedpubkey</c> names it, such as
    /// <c>sha256//7VmZ...=</c>, which curl prints as <c>-v</c>'s <c> public key hash:</c> line,
    /// or <see langword="null" /> when no <c>sha256//</c> pin was checked (ADR-0336, BL-877).
    /// </summary>
    public string? PinnedPublicKeyHash { get; init; }

    /// <summary>
    /// Gets a value indicating whether the handshake failed, so that only the lines a curl build
    /// prints before its failure are printed: the Schannel build's ALPN offer and
    /// <c> public key hash:</c> line, all of the OpenSSL build's (ADR-0363, BL-1149).
    /// </summary>
    public bool Failed { get; init; }
}
