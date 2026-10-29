using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Extension.EncryptedDns.DnsOverQuic;

/// <summary>
/// Hands the chain a DNS-over-QUIC server presents to <see cref="ITlsProvider.VerifyServerCertificateChain" />
/// (ADR-1052), so the QUIC handshake judges it as curl's own TLS judges a certificate.
/// </summary>
/// <param name="tlsProvider">Judges the chain.</param>
/// <param name="targetHost">The name the server's certificate is checked against.</param>
internal sealed class TlsProviderCertificateVerifier(ITlsProvider tlsProvider, string targetHost) : IServerCertificateVerifier
{
    /// <inheritdoc />
    public ServerCertificateVerdict Verify(ServerCertificateChain presented)
    {
        ArgumentNullException.ThrowIfNull(presented);
        ServerCertificateChainVerdict verdict = tlsProvider.VerifyServerCertificateChain([.. presented.Certificates.Select(certificate => (ReadOnlyMemory<byte>)certificate)], targetHost);
        return verdict.IsAccepted ? ServerCertificateVerdict.Accepted : ServerCertificateVerdict.Rejected(verdict.ErrorMessage!);
    }
}
