using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Extension.Protocol.WebTransport.Session;

/// <summary>
/// Hands the chain a WebTransport server presents in the QUIC handshake to
/// <see cref="ITlsProvider.VerifyServerCertificateChain" />, so it is judged as curl's own TLS judges one.
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
